using System.Reflection;
using System.Runtime.CompilerServices;
using Momentum.Api.Endpoints;
using Momentum.Application.Abstractions;
using Momentum.Domain;
using Momentum.Infrastructure;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Momentum.ArchitectureTests;

/// <summary>
/// ADR 0001 K-A1/K-H1 architecture rules (GOREV slice-1 D9). Each rule is a real
/// TYPE-dependency check via NetArchTest (Mono.Cecil), not a mere project-reference check;
/// D10 proves each rule bites by introducing a real type dependency (captured in KANIT).
/// </summary>
public sealed class ArchitectureRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(DomainAssemblyReference).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ICurrentUser).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureAssemblyReference).Assembly;
    private static readonly Assembly ApiAssembly = typeof(HealthEndpoints).Assembly;

    [Fact]
    public void Sanity_NetArchTest_loads_the_net9_assemblies()
    {
        // If NetArchTest cannot load the .NET 9 assemblies it returns no types -> report loudly
        // (GOREV slice-1 D9: do not silently drop a rule).
        Types.InAssembly(ApplicationAssembly).GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(InfrastructureAssembly).GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(DomainAssembly).GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(ApiAssembly).GetTypes().ShouldNotBeEmpty();
    }

    [Fact]
    public void Rule1_Application_must_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Momentum.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Rule2_Api_endpoints_must_not_depend_on_Infrastructure_concrete_types()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Momentum.Api.Endpoints")
            .ShouldNot()
            .HaveDependencyOn("Momentum.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Rule3_Domain_must_not_depend_on_EfCore_AspNetCore_or_Npgsql()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    /// <summary>
    /// slice-2b2 D9-b: written as a cheap regression, but NOT counted as a bitten gate. The rule is
    /// tautological on THIS project shape -- Momentum.Infrastructure.csproj is plain <c>Microsoft.NET.Sdk</c>
    /// (no <c>FrameworkReference Microsoft.AspNetCore.App</c>), so SignalR types are not even reachable
    /// from there; no production-code mutation could make this rule fail. Reported honestly as such.
    /// </summary>
    [Fact]
    public void Rule4_Infrastructure_must_not_depend_on_SignalR()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.SignalR")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §G (mekanik sinif kapisi, ISLEYIS md.8): SyncPuller.cs ve
    /// ScopeMembershipSource.cs kaynaklarinda "project_members" dizgesi GECMEZ -- uyelik erisimi
    /// YALNIZ `project_access` GORUNUMU uzerindendir (sahip project_members'e YAZILMAZ, §C3;
    /// dogrudan project_members sorgulamak sahibi KACIRIR, bulgu 1). NetArchTest tur-bagimliligi
    /// degil KAYNAK METNI sinar -- bu kapi bir dizge kapisidir.
    /// </summary>
    [Fact]
    public void Rule5_Sync_read_paths_reach_membership_only_through_the_project_access_view()
    {
        var repoRoot = FindRepoRoot();
        var syncPuller = File.ReadAllText(Path.Combine(repoRoot, "src", "backend", "Momentum.Infrastructure", "Sync", "SyncPuller.cs"));
        var scopeMembershipSource = File.ReadAllText(Path.Combine(repoRoot, "src", "backend", "Momentum.Infrastructure", "Sync", "ScopeMembershipSource.cs"));

        syncPuller.ShouldNotContain("project_members", customMessage: "SyncPuller.cs project_access GORUNUMU yerine dogrudan project_members'i sorguluyor");
        scopeMembershipSource.ShouldNotContain("project_members", customMessage: "ScopeMembershipSource.cs project_access GORUNUMU yerine dogrudan project_members'i sorguluyor");
    }

    private static string FindRepoRoot([CallerFilePath] string here = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Momentum.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Momentum.sln bulunamadi (FindRepoRoot).");
    }

    private static string FailureMessage(TestResult result)
    {
        var failing = result.FailingTypeNames is null ? "(none)" : string.Join(", ", result.FailingTypeNames);
        return $"Architecture rule violated. Failing types: {failing}";
    }
}
