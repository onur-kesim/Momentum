using System.Text.Json;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;
using Momentum.Domain.Sync;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>
/// GOREV-slice-3d G7/D9 -- `owner_id` KUSURU düzeltmesinin çekme GÖRÜNÜRLÜĞÜ üzerindeki etkisi
/// (gerçek PostgreSQL, Testcontainers). `ScopeAndDriftAnchorTests.D9_...` SQL sütunlarını doğrudan
/// ölçer; bu dosya AYNI senaryoyu `SyncPuller`in `owner_id` süzgeci ÜZERİNDEN, uçtan uca ölçer.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class D9OwnerIdVisibilityTests(PostgresFixture fixture)
{
    /// <summary>
    /// IS-EMRI-o86-A §A1 (ILK IS, kod yazilmadan ONCE kosuldu) + §B/G1: bir `Project` op'unun
    /// `scope_id`si kendi `entityId`sidir (proje scope'un kendisidir), `old_scope_id` her zaman null
    /// (bir projenin scope'u degismez). §B uygulanmadan once bu test KIRMIZI dustu (scope_id NULL
    /// cikti) -- ham cikti KANIT/o86A/01-olcum.txt'e alindi, tasarimin varsayimini (postProjectId bir
    /// Project op'unda null'dur) DOGRULADI. §B'nin mutant-ispati da AYNI testtir (G1).
    /// </summary>
    [Fact]
    public async Task Project_op_scope_id_kendi_entity_id_sidir_old_scope_id_her_zaman_null()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actor = Guid.NewGuid();
        var projectEntity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        await app.SyncAsync(actor, Wire.PushNoPull(actor, Wire.Op(opId, actor, projectEntity, actor, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(actor, 1)) },
            entityType: "Project")));

        var scopeId = await Db.ScalarAsync<Guid?>(connectionString, "SELECT scope_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));
        var oldScopeId = await Db.ScalarAsync<Guid?>(connectionString, "SELECT old_scope_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));

        scopeId.ShouldBe(projectEntity, "Project op'unun scope_id'si kendi entityId'sidir");
        oldScopeId.ShouldBeNull("bir projenin scope'u degismez");
    }

    [Fact]
    public async Task D9_baslik_X_govde_actorId_Y_ise_satir_Xin_cekmesinde_gorunur_Yninkinde_gorunmez()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Baslik (kimlik dogrulama) X'tir; govdenin actorId'si Y'dir (enjeksiyon senaryosu).
        await app.SyncAsync(actorX, Wire.PushNoPull(actorX, Wire.TaskField(opId, actorX, entity, actorY, "title", "D9 gorunurluk testi")));

        var xSnapshot = await app.SnapshotAsync(actorX);
        xSnapshot.Entities.ShouldContain(e => e.EntityId == entity, "D9: outbox owner_id KIMLIK DOGRULANMIS X'tir -- X'in cekmesinde GORUNMELI");

        var ySnapshot = await app.SnapshotAsync(actorY);
        ySnapshot.Entities.ShouldNotContain(e => e.EntityId == entity, "D9: govdenin actorId iddiasi (Y) outbox owner_id'yi BELIRLEMEMELI -- Y'nin cekmesinde GORUNMEMELI");
    }

    /// <summary>
    /// IS-EMRI-o83 F5 KILIDI: D9'un onceki beyanini SUPERSEDE eder. Eskiden ("D9 BEYAN") actor_id
    /// (denetim kaydi) BILEREK govdeden geliyordu; artik GERCEK kullanicilar var ve govdenin actorId
    /// iddiasini denetim kaydinda bile serbest birakmak bir kullaniciya baskasinin degisikligini
    /// YUKLEYEBILMEK demek (dilim 3 isbirliginde sahtekarlik riski) -- bu yuzden actor_id de artik
    /// KIMLIK DOGRULANMIS aktorden yazilir, govdenin (Y) iddiasi YOK SAYILIR.
    /// </summary>
    [Fact]
    public async Task D9_outbox_owner_id_VE_actor_id_ikisi_de_artik_dogrulanan_aktorden_yazilir()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Baslik (kimlik dogrulama) X'tir; govdenin actorId'si Y'dir (enjeksiyon senaryosu, degismedi).
        await app.SyncAsync(actorX, Wire.PushNoPull(actorX, Wire.TaskField(opId, actorX, entity, actorY, "title", "F5 SQL dogrulamasi")));

        var ownerId = await Db.ScalarAsync<Guid>(connectionString, "SELECT owner_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));
        var actorId = await Db.ScalarAsync<Guid>(connectionString, "SELECT actor_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));

        // GOREV-slice-3d 8.2 deseni korunur: bu sorgu Testcontainers icinde kosar, PowerShell o
        // baglantiyi GOREMEZ -- KANITI ayagin KENDISI yazar. Ortam degiskeni yoksa SESSIZCE atlamak
        // YASAK, test firlatir.
        var kanitDizini = Environment.GetEnvironmentVariable("MOMENTUM_KANIT_DIZIN")
            ?? throw new InvalidOperationException("MOMENTUM_KANIT_DIZIN ortam degiskeni ayarli degil -- KANIT sessizce atlanamaz.");
        Directory.CreateDirectory(kanitDizini);
        await File.WriteAllTextAsync(Path.Combine(kanitDizini, "outbox-sorgu.txt"),
            $"SELECT owner_id, actor_id FROM outbox_messages WHERE operation_id = @op (op={opId})\n" +
            $"actorX (kimlik dogrulama basligi) = {actorX}\n" +
            $"actorY (govdenin actorId iddiasi, ARTIK EZILIYOR) = {actorY}\n" +
            $"owner_id  = {ownerId}  (beklenen: actorX)\n" +
            $"actor_id  = {actorId}  (beklenen: actorX -- o83 F5, D9'u supersede eder)\n");

        ownerId.ShouldBe(actorX, "owner_id KIMLIK DOGRULAMADAN gelir");
        actorId.ShouldBe(actorX, "IS-EMRI-o83 F5: actor_id de artik KIMLIK DOGRULANMIS aktorden yazilir -- govdenin (Y) iddiasi yok sayilir");
    }

    /// <summary>
    /// IS-EMRI-o83 F5 -- ikinci yari: bayt-duzeyinde OutboxRecord.ActorId (DB sutunu) duzeltilse bile,
    /// WireMapping.ClampedPayload govdenin actorId'sini AYRICA yankiliyor olabilirdi -- bu payload,
    /// BASKA istemcilerin pull ile GERCEKTEN okudugu JSON'un ta kendisidir (WireChange.Payload). Bu
    /// test DB sutununu DEGIL, tam da o yankilanan payload'in icindeki actorId alanini olcer.
    /// </summary>
    [Fact]
    public async Task F5_pull_payloadindaki_WireOp_ActorId_de_authenticated_aktorden_gelir_govdeden_DEGIL()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Bos taban imlec (entity henuz yok), sonra AYNI istekte push+pull -- X kendi yazdigi
        // degisikligi "changes" olarak geri okur (SyncCommandHandler: push ONCE, pull SONRA, ayni istek).
        var taban = await app.SnapshotAsync(actorX);
        var yanit = await app.SyncAsync(actorX, new SyncRequest(actorX, null,
            new WireCursor(taban.NextCursor.Xid, taban.NextCursor.Seq),
            [Wire.TaskField(opId, actorX, entity, actorY, "title", "F5 payload testi")]));

        var degisiklik = yanit.Changes.ShouldHaveSingleItem();
        degisiklik.Payload.GetProperty("actorId").GetGuid()
            .ShouldBe(actorX, "IS-EMRI-o83 F5: pull payload'indaki WireOp.ActorId de kimlik dogrulanmis aktorden gelir, govdenin (Y) iddiasi DEGIL");
    }

    /// <summary>
    /// IS-EMRI-o86-A G3+G4: uye B hem ARTIMLI hem TAZE-KURULUM (snapshot) yolunda projeyi VE gorevini
    /// GORUR; uye OLMAYAN C HICBIRINDE gormez (pozitif+negatif, o83-G dersi -- bos liste her iddiayi gecirir).
    /// </summary>
    [Fact]
    public async Task Member_sees_project_and_task_via_incremental_and_snapshot_non_member_sees_nothing()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var nonMember = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        // Member'in baseline cursor'u -- uyelik/gorev DOGMADAN once (artimlinin GERCEKTEN yeni geleni
        // getirdigini kanitlamak icin).
        var baseline = await app.SnapshotAsync(member);

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // G3: artimli pull -- UYE GORUR.
        var memberPage = await app.PullAsync(member, baseline.NextCursor);
        var memberEntities = memberPage.Changes
            .Select(c => JsonDocument.Parse(c.PayloadJson).RootElement.GetProperty("entityId").GetGuid())
            .ToHashSet();
        memberEntities.ShouldContain(project);
        memberEntities.ShouldContain(task);

        // G3 POZITIF KONTROL: uye OLMAYAN C GORMEZ.
        var nonMemberBaseline = await app.SnapshotAsync(nonMember);
        var nonMemberPage = await app.PullAsync(nonMember, nonMemberBaseline.NextCursor);
        nonMemberPage.Changes.ShouldBeEmpty();

        // G4: TAZE KURULUM snapshot -- UYE projeyi VE gorevi GORUR.
        var memberSnapshot = await app.SnapshotAsync(member);
        var memberSnapshotIds = memberSnapshot.Entities.Select(e => e.EntityId).ToHashSet();
        memberSnapshotIds.ShouldContain(project);
        memberSnapshotIds.ShouldContain(task);

        // G4 POZITIF KONTROL: uye OLMAYAN C'nin taze kurulumu BOS.
        var nonMemberSnapshot = await app.SnapshotAsync(nonMember);
        nonMemberSnapshot.Entities.ShouldBeEmpty();
    }

    /// <summary>
    /// IS-EMRI-o86-A G5 (PAZARLIKSIZ): bir gorev projeden CIKARILINCA (projectId -> null), o degisiklik
    /// UYEYE old_scope_id UZERINDEN ULASIR -- bu kol dusseydi uyenin ekraninda hayalet satir kalirdi.
    /// </summary>
    [Fact]
    public async Task Task_removed_from_project_reaches_member_via_old_scope_id()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        // Uyenin BASLANGIC cursor'u -- her sey olmadan ONCE (snapshot ONCE alinirsa, ondan sonraki
        // pull, snapshot'in KENDISI zaten kapsadigi icin yanlislikla BOS doner -- pazarliksiz sira budur).
        var baseline = await app.SnapshotAsync(member);

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // Uyenin cursor'unu gorevin P'ye GIRISINI GORMUS noktaya ilerlet.
        var afterCreate = await app.PullAsync(member, baseline.NextCursor);
        afterCreate.Changes.ShouldNotBeEmpty();

        // Gorev Gelen Kutusu'na tasinir (projectId -> null): scope_id NULL, old_scope_id = P.
        var moveOpId = Guid.CreateVersion7();
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(moveOpId, owner, task, owner, "projectId", null, counter: 4)));

        var afterMove = await app.PullAsync(member, afterCreate.NextCursor);
        var degisiklik = afterMove.Changes.ShouldHaveSingleItem();
        JsonDocument.Parse(degisiklik.PayloadJson).RootElement.GetProperty("entityId").GetGuid().ShouldBe(task);

        (await Db.ScalarAsync<Guid>(connectionString, "SELECT old_scope_id FROM outbox_messages WHERE operation_id = @o", ("o", moveOpId)))
            .ShouldBe(project);
    }

    /// <summary>
    /// IS-EMRI-o86-A G6: uye OLMAYANIN (mevcut scope'lu Task) yazimi RED · uyenin `members` yazimi RED
    /// (en sert kural -- sahip-yalniz, aksi halde bir uye sahibi silip projeyi CALARDI) · sahibin
    /// `members` yazimi KABUL.
    /// </summary>
    [Fact]
    public async Task Write_authorization_rejects_non_member_and_member_membership_write_accepts_owner()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // Uye OLMAYAN (outsider), P kapsamindaki gorevi yazmaya calisir -- RED.
        var outsiderResponse = await app.SyncAsync(outsider, Wire.PushNoPull(outsider,
            Wire.TaskField(Guid.CreateVersion7(), outsider, task, outsider, "title", "calindi", counter: 1)));
        outsiderResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
        (await Db.ScalarAsync<string>(connectionString, "SELECT title FROM tasks WHERE entity_id = @e", ("e", task)))
            .ShouldBeNull("outsider'in yazimi materyalize OLMAMALI");

        // UYE (member), members setine yazmaya calisir (baskasini eklemeye) -- RED (sahip-yalniz kural).
        var memberResponse = await app.SyncAsync(member, Wire.PushNoPull(member, Wire.Op(Guid.CreateVersion7(), member, project, member, 4,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(outsider.ToString(), Guid.CreateVersion7(), Wire.Hlc(member, 4))], null),
            },
            entityType: "Project")));
        memberResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @o", ("p", project), ("o", outsider)))
            .ShouldBe(0L, "uyenin members yazimi ETKISIZ kalmali");

        // SAHIP (owner), members'a yazar -- KABUL.
        var ownerResponse = await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 5,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(outsider.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 5))], null),
            },
            entityType: "Project")));
        ownerResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.Applied));
        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @o", ("p", project), ("o", outsider)))
            .ShouldBe(1L);
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §E H1 (🔴 BU DILIMIN ASIL KAPISI, bulgu 1): SAHIP A, UYE B'nin yazdigi
    /// degisikligi kendi ARTIMLI (incremental) pull'unda GORUR -- eskiden sahip project_members'e
    /// YAZILMADIGI icin (§C3) kendi projesindeki UYE-yazili degisiklikleri GOREMIYORDU (outbox
    /// satirinin owner_id'si YAZAN'dir, projenin sahibi degil). POZITIF KONTROL: uye OLMAYAN C
    /// hicbir sey GORMEZ.
    /// </summary>
    [Fact]
    public async Task Owner_sees_members_write_via_incremental_pull_non_member_sees_nothing_H1()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var nonMember = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // SAHIP'in GERCEK bir imlecten (0,0) baslayan artimli pull'u -- snapshot-horizon karisikligina
        // GIRMEDEN dogrudan PullIncrementalAsync'i cagirir (app.PullAsync, TestSupport.cs).
        var ownerCursor = (await app.PullAsync(owner, new SyncCursor(0, 0))).NextCursor;

        // UYE (member), gorevi duzenler.
        await app.SyncAsync(member, Wire.PushNoPull(member,
            Wire.TaskField(Guid.CreateVersion7(), member, task, member, "title", "Uye duzenledi", counter: 1)));

        var afterEdit = await app.PullAsync(owner, ownerCursor);
        var ownerGorurMu = afterEdit.Changes.Any(c =>
            JsonDocument.Parse(c.PayloadJson).RootElement.GetProperty("entityId").GetGuid() == task);
        ownerGorurMu.ShouldBeTrue("H1: SAHIP, UYENIN yazdigini ARTIMLI pull'da GORMELI");

        // POZITIF KONTROL: uye OLMAYAN C hicbir sey gormez.
        var nonMemberPull = await app.PullAsync(nonMember, new SyncCursor(0, 0));
        nonMemberPull.Changes.ShouldBeEmpty();
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §E H3: uyelikten CIKARILMIS B, T'yi `projectId=null` yaparak KOPARAMAZ ⇒
    /// RejectedForbidden. POZITIF KONTROL: hala UYE olan digeri AYNI turden bir op'u yapabilir.
    /// </summary>
    [Fact]
    public async Task Ex_member_cannot_detach_task_still_member_can_H3()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var exMember = Guid.NewGuid();
        var stillMember = Guid.NewGuid();
        var project = Guid.NewGuid();
        var taskForExMember = Guid.NewGuid();
        var taskForStillMember = Guid.NewGuid();
        var exMemberTag = Guid.CreateVersion7();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(
                    [
                        new WireSetAdd(exMember.ToString(), exMemberTag, Wire.Hlc(owner, 2)),
                        new WireSetAdd(stillMember.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2)),
                    ], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, taskForExMember, owner, "projectId", project.ToString(), counter: 3)));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, taskForStillMember, owner, "projectId", project.ToString(), counter: 4)));

        // owner exMember'i uyelikten CIKARIR.
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 5,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(null, [new WireSetRemove(exMember.ToString(), [exMemberTag], Wire.Hlc(owner, 5))]),
            },
            entityType: "Project")));

        // ESKI uye exMember, kendi (eskiden erisimi olan) gorevini Gelen Kutusu'na KOPARMAYA calisir -- RED.
        // wallOffset: HLC'nin wallMs'i sabit BaseWall'dir (gercek saat degil) -- owner'in "projectId"yi
        // KOYAN daha ONCEKI op'u (counter:3, wallOffset:0) ile AYNI wallMs'te kalirsak, LWW'nin
        // sayac-esitligi kirilma kurali (tie-break) exMember'in KUCUK sayacini (1) DEGIL owner'in
        // BUYUK sayacini (3) kazandirir -- yani alan HICBIR ZAMAN gercekten null'a DONMEZ ve red baska
        // (kazara dogru) bir sebepten cikar, mutant hicbir sey degistirmez. wallOffset ile bu op'u
        // gercekten KRONOLOJIK OLARAK SONRA yapar.
        var rEx = await app.SyncAsync(exMember, Wire.PushNoPull(exMember,
            Wire.TaskField(Guid.CreateVersion7(), exMember, taskForExMember, exMember, "projectId", null, counter: 1, wallOffset: 100)));
        rEx.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));

        // POZITIF KONTROL: hala UYE olan stillMember AYNI turden bir op'u yapabilir (ayni HLC nedeni).
        var rStill = await app.SyncAsync(stillMember, Wire.PushNoPull(stillMember,
            Wire.TaskField(Guid.CreateVersion7(), stillMember, taskForStillMember, stillMember, "projectId", null, counter: 1, wallOffset: 100)));
        rStill.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.Applied));
    }

    /// <summary>IS-EMRI-o86-A2 §E H4: uyelikten cikarilmis B, T'yi KENDI projesi Q'ya TASIYAMAZ ⇒ RejectedForbidden.</summary>
    [Fact]
    public async Task Ex_member_cannot_move_task_to_own_project_H4()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var exMember = Guid.NewGuid();
        var projectP = Guid.NewGuid();
        var projectQ = Guid.NewGuid();
        var task = Guid.NewGuid();
        var exMemberTag = Guid.CreateVersion7();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, projectP, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, projectP, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(exMember.ToString(), exMemberTag, Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", projectP.ToString(), counter: 3)));
        // exMember KENDI projesi Q'yu yaratir (Q'nun GERCEK sahibi -- IZIN(Q) exMember icin HER ZAMAN true).
        await app.SyncAsync(exMember, Wire.PushNoPull(exMember, Wire.Op(Guid.CreateVersion7(), exMember, projectQ, exMember, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("Q", Wire.Hlc(exMember, 1)) },
            entityType: "Project")));

        // owner exMember'i P'nin uyeliginden CIKARIR.
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, projectP, owner, 5,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(null, [new WireSetRemove(exMember.ToString(), [exMemberTag], Wire.Hlc(owner, 5))]),
            },
            entityType: "Project")));

        // ESKI uye exMember, T'yi KENDI projesi Q'ya tasimaya calisir -- IZIN(post=Q) true olsa BILE
        // IZIN(pre=P) artik false oldugu icin RED (IZIN(pre) VE IZIN(post) formulu).
        // wallOffset: H3'teki AYNI HLC tie-break sebebiyle -- owner'in "projectId"yi P'ye KOYAN
        // op'u (counter:3, wallOffset:0) ile ayni wallMs'te kalirsak sayac-esitligi kirilma kurali
        // exMember'in DAHA KUCUK sayacini (2) degil owner'in BUYUGUNU (3) kazandirir, alan asla Q'ya
        // GECMEZ ve red baska (kazara dogru) bir sebepten cikar.
        var r = await app.SyncAsync(exMember, Wire.PushNoPull(exMember,
            Wire.TaskField(Guid.CreateVersion7(), exMember, task, exMember, "projectId", projectQ.ToString(), counter: 2, wallOffset: 100)));
        r.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §E H5: yabanci C, TAHMIN ETTIGI bir `projectId=P` ile YEPYENI bir gorev
    /// DOGURAMAZ ⇒ RejectedForbidden. POZITIF KONTROL: GERCEK uye B AYNI op turuyle yeni gorev dogurabilir.
    /// </summary>
    [Fact]
    public async Task Stranger_cannot_inject_new_task_into_guessed_project_member_can_H5()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var project = Guid.NewGuid();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));

        // Yabanci, TAHMIN ETTIGI projectId=P ile YEPYENI bir gorev yaratmaya calisir -- RED.
        var strangerTask = Guid.NewGuid();
        var rStranger = await app.SyncAsync(stranger, Wire.PushNoPull(stranger,
            Wire.TaskFields(Guid.CreateVersion7(), stranger, strangerTask, stranger, 1, ("title", "enjekte"), ("projectId", project.ToString()))));
        rStranger.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM tasks WHERE entity_id = @e", ("e", strangerTask)))
            .ShouldBe(0L, "enjekte edilen gorev materyalize OLMAMALI");

        // POZITIF KONTROL: GERCEK uye, AYNI turden (yeni gorev + projectId TEK op'ta) bir op yapabilir.
        var memberTask = Guid.NewGuid();
        var rMember = await app.SyncAsync(member, Wire.PushNoPull(member,
            Wire.TaskFields(Guid.CreateVersion7(), member, memberTask, member, 1, ("title", "uye yeni gorev"), ("projectId", project.ToString()))));
        rMember.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.Applied));
    }

    /// <summary>
    /// IS-EMRI-o86-A2 4. bulgu (Onur kilidi, o86-A2 canli tur adim 9'da bulundu, is emrinin KENDI
    /// H1-H6 setinin DISINDA): eski `PullIncrementalAsync`nin `owner_id = @actorId` kolu bir op'u
    /// YAZDIGI outbox satirini (append-only, degismez) SONSUZA DEK gorunur tutuyordu -- uye
    /// CIKARILDIKTAN SONRA bile, UYEYKEN yazdigi (scope tasiyan) kendi eski satiri kendi
    /// `owner_id`siyle eslesmeye devam ediyordu. Duzeltme: bu kol artik YALNIZ scope'suz (kisisel,
    /// Gelen Kutusu) satirlarda gecerli -- scope tasiyan bir satinin gorunurlugu SADECE GUNCEL
    /// uyelikten (project_access) gelir, o satiri KIMIN yazdigindan degil.
    /// H7a: ARTIMLI (incremental) kanal. POZITIF KONTROL: B UYEYKEN kendi duzenlemesini kendi
    /// ARTIMLI pull'unda gorur. ANA IDDIA: B CIKARILDIKTAN SONRA, AYNI (eski) cursor'dan tekrar
    /// pull yapinca kendi eski duzenleme op'unu ARTIK GORMEZ.
    /// </summary>
    [Fact]
    public async Task Ex_member_own_past_scoped_write_no_longer_grants_incremental_visibility_H7a()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();
        var memberTag = Guid.CreateVersion7();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), memberTag, Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // B'nin KENDI baslangic cursor'u -- eski (uyelikten cikarilmadan ONCEKI) bir horizon, kasitli
        // olarak SAKLANIR: ana iddia bu AYNI eski cursor'dan TEKRAR pull yapmaktir.
        var bCursorEski = (await app.PullAsync(member, new SyncCursor(0, 0))).NextCursor;

        // B, UYEYKEN, T'yi duzenler -- outbox satiri owner_id=B, scope_id=P (GERCEK hatanin ta kendisi).
        await app.SyncAsync(member, Wire.PushNoPull(member,
            Wire.TaskField(Guid.CreateVersion7(), member, task, member, "title", "Uye duzenledi", counter: 1)));

        // POZITIF KONTROL: B, HALA UYEYKEN, KENDI eski cursor'undan ARTIMLI pull'da kendi
        // duzenlemesini GORUR (scope_id IN project_access(B) uzerinden -- owner_id kolu GEREKMEZ).
        var whileMember = await app.PullAsync(member, bCursorEski);
        whileMember.Changes.Any(c => JsonDocument.Parse(c.PayloadJson).RootElement.GetProperty("entityId").GetGuid() == task)
            .ShouldBeTrue("H7a POZITIF KONTROL: B UYEYKEN kendi duzenlemesini ARTIMLI pull'da GORMELI");

        // owner, B'yi uyelikten CIKARIR.
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 4,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(null, [new WireSetRemove(member.ToString(), [memberTag], Wire.Hlc(owner, 4))]),
            },
            entityType: "Project")));

        // ANA IDDIA: B, AYNI eski cursor'dan (bCursorEski) TEKRAR pull yapar -- KENDI eski
        // duzenlemesi ARTIK GORUNMEMELI (owner_id kolu artik gecersiz, scope_id IN project_access(B)
        // de artik false -- B cikarildi).
        var afterRemoval = await app.PullAsync(member, bCursorEski);
        afterRemoval.Changes.Any(c => JsonDocument.Parse(c.PayloadJson).RootElement.GetProperty("entityId").GetGuid() == task)
            .ShouldBeFalse("H7a ANA IDDIA: B CIKARILDIKTAN SONRA kendi ESKI duzenlemesini ARTIK GORMEMELI");
    }

    /// <summary>
    /// IS-EMRI-o86-A2 4. bulgu, H7b: SNAPSHOT kanali. Onur'un ISARET ETTIGI EN SIKI senaryo: B
    /// gorevi ONCE KENDI Gelen Kutusu'nda (scope NULL/NULL) yaratir, SONRA P'ye TASIR -- bu eski
    /// KISISEL outbox satiri (scope_id IS NULL) narrow edilmis outbox-tabanli bir kural bile
    /// GECERDI. Dogru olcum GUNCEL materyalize durumdur (tasks.project_id/owner_id) -- bu test
    /// TAM O SENARYOYU sinar. POZITIF KONTROL: B UYEYKEN snapshot'ta gorevi gorur. ANA IDDIA: B
    /// CIKARILDIKTAN SONRA ayni gorev snapshot'ta ARTIK YOK.
    /// </summary>
    [Fact]
    public async Task Ex_member_task_created_personally_then_moved_into_project_disappears_from_snapshot_after_removal_H7b()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();
        var memberTag = Guid.CreateVersion7();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), memberTag, Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));

        // B, T'yi ONCE KENDI Gelen Kutusu'nda yaratir (scope NULL/NULL, outbox owner_id=B).
        await app.SyncAsync(member, Wire.PushNoPull(member,
            Wire.TaskField(Guid.CreateVersion7(), member, task, member, "title", "Kisisel gorev", counter: 1)));
        // B, SONRA T'yi P'ye TASIR (uye oldugu icin IZIN(post=P) true) -- guncel materyalize durum
        // artik tasks.project_id=P, ama ESKI kisisel yaratim satiri outbox'ta scope NULL/NULL KALIR.
        await app.SyncAsync(member, Wire.PushNoPull(member,
            Wire.TaskField(Guid.CreateVersion7(), member, task, member, "projectId", project.ToString(), counter: 2)));

        // POZITIF KONTROL: B, HALA UYEYKEN, snapshot'ta T'yi GORUR.
        var whileMember = await app.SnapshotAsync(member);
        whileMember.Entities.ShouldContain(e => e.EntityId == task, "H7b POZITIF KONTROL: B UYEYKEN T snapshot'ta GORULMELI");

        // owner, B'yi uyelikten CIKARIR.
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 3,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(null, [new WireSetRemove(member.ToString(), [memberTag], Wire.Hlc(owner, 3))]),
            },
            entityType: "Project")));

        // ANA IDDIA: B, CIKARILDIKTAN SONRA, snapshot'ta T'yi ARTIK GORMEZ -- ESKI kisisel yaratim
        // satiri (scope NULL/NULL) entity listesine SIZDIRMAMALI (materyalize tasks.project_id
        // uzerinden olculur, outbox GECMISI degil).
        var afterRemoval = await app.SnapshotAsync(member);
        afterRemoval.Entities.ShouldNotContain(e => e.EntityId == task, "H7b ANA IDDIA: B CIKARILDIKTAN SONRA T'yi ARTIK GORMEMELI");
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §E H6 (REGRESYON, mutantsiz -- ISLEYIS md.8): sahip kendi Gelen Kutusu'na
    /// (scope null) yeni gorev yazabilir -- §C/§D'nin pre/post-scope daralmasi Inbox'i ETKILEMEDI.
    /// </summary>
    [Fact]
    public async Task Owner_can_still_write_to_own_inbox_H6()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var task = Guid.NewGuid();

        var r = await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskFields(Guid.CreateVersion7(), owner, task, owner, 1, ("title", "Gelen Kutusu gorevi"))));
        r.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.Applied));
        (await Db.ScalarAsync<string>(connectionString, "SELECT title FROM tasks WHERE entity_id = @e", ("e", task)))
            .ShouldBe("Gelen Kutusu gorevi");
    }

    /// <summary>
    /// IS-EMRI-o86-A3 §B: sinifi (yetki kararinin (isNew, preScope, postScope) -> karar SAF
    /// FONKSIYONUNU) MEKANIKLESTIREN doğruluk tablosu -- IŞLEYIŞ md.8. Eksenler:
    ///   preCase ("isNew" eksenini de tasir, cunku yeni varlikta preScope ANLAMSIZDIR):
    ///     Yeni                -> varlik hic hidratlanmamis (IZIN_PRE=true, preScope SORULMAZ)
    ///     PreNullBenim        -> mevcut gorev, Gelen Kutusu'nda, actor GERCEK (materyalize) sahip
    ///     PreNullBaskasinin   -> mevcut gorev, Gelen Kutusu'nda, BASKASI sahip (bulgu 5'in ta kendisi)
    ///     PreScopeUyeyim      -> mevcut gorev, P'ye scope'lu, actor P'nin HALA UYESI (H3 pozitif kontrol)
    ///     PreScopeUyeDegilim  -> mevcut gorev, P'ye scope'lu, actor P'nin UYESI DEGIL (H3/H4'un sinifi)
    ///   postCase:
    ///     PostNull            -> hedef Gelen Kutusu (IZIN_POST=true KOSULSUZ -- asimetri, DOKUNMA LISTESI)
    ///     PostSahibim         -> hedef Q, actor Q'nun GERCEK sahibi
    ///     PostUyeDegilim      -> hedef Q, actor Q'nun UYESI DEGIL
    /// "Yeni + preScope != null" ANLAMSIZ bilesimi (yeni varlikta gecmis baglam yoktur) hic
    /// InlineData'ya ALINMADI -- yalniz "Yeni" + 3 postCase (preScope SORULMADIGI icin tek eksenli).
    /// Diger 4 preCase x 3 postCase TAM CARPIM (12 satir) + 3 "Yeni" satiri = 15 satir.
    /// </summary>
    [Theory]
    [InlineData("Yeni", "PostNull", "Applied")] // yeni gorev, Gelen Kutusu'nda dogar -- IZIN_PRE=true (yeni), IZIN_POST(null)=true
    [InlineData("Yeni", "PostSahibim", "Applied")] // yeni gorev + actor'un KENDI projesi Q -- H5 pozitif kontrolun ayni sinifi
    [InlineData("Yeni", "PostUyeDegilim", "RejectedForbidden")] // H5 ana iddia: yabanci, tahmin ettigi Q ile yeni gorev enjekte edemez
    [InlineData("PreNullBenim", "PostNull", "Applied")] // actor KENDI mevcut Gelen Kutusu gorevini duzenler (regresyon, gercek sahiplik)
    [InlineData("PreNullBenim", "PostSahibim", "Applied")] // actor kendi Gelen Kutusu gorevini KENDI projesine tasir
    [InlineData("PreNullBenim", "PostUyeDegilim", "RejectedForbidden")] // actor kendi gorevini UYESI OLMADIGI bir projeye tasiyamaz (IZIN_POST kapisi)
    [InlineData("PreNullBaskasinin", "PostNull", "RejectedForbidden")] // BULGU 5 (canli tur adim 13): yabanci baskasinin Gelen Kutusu gorevine YAZAMAZ
    [InlineData("PreNullBaskasinin", "PostSahibim", "RejectedForbidden")] // BULGU 5 (canli tur adim 14): yabanci baskasinin gorevini KENDI projesine CALAMAZ
    [InlineData("PreNullBaskasinin", "PostUyeDegilim", "RejectedForbidden")] // cifte yetkisiz -- IZIN_PRE zaten tek basina yeter
    [InlineData("PreScopeUyeyim", "PostNull", "Applied")] // H3 POZITIF KONTROL: hala uye, gorevi Gelen Kutusu'na KOPARABILIR
    [InlineData("PreScopeUyeyim", "PostSahibim", "Applied")] // hala uye, gorevi KENDI baska projesine MESRU tasir
    [InlineData("PreScopeUyeyim", "PostUyeDegilim", "RejectedForbidden")] // hala uye ama HEDEFTE uye degil -- IZIN_POST kapisi
    [InlineData("PreScopeUyeDegilim", "PostNull", "RejectedForbidden")] // H3 ANA IDDIA: eski uye gorevi Gelen Kutusu'na KOPARAMAZ
    [InlineData("PreScopeUyeDegilim", "PostSahibim", "RejectedForbidden")] // H4 ANA IDDIA: eski uye gorevi KENDI projesine CALAMAZ
    [InlineData("PreScopeUyeDegilim", "PostUyeDegilim", "RejectedForbidden")] // cifte yetkisiz -- IZIN_PRE zaten tek basina yeter
    public async Task Dogruluk_tablosu_IZIN_PRE_IZIN_POST_H8(string preCase, string postCase, string expectedCode)
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actor = Guid.NewGuid();
        var task = Guid.NewGuid();

        // --- preScope kurulumu ("Yeni" ise T hic yaratilmaz -- ACT adimi onu ilk kez dogurur) ---
        if (preCase != "Yeni")
        {
            switch (preCase)
            {
                case "PreNullBenim":
                    // actor KENDI Gelen Kutusu gorevini yaratir -- ilk yazan = materyalize tasks.owner_id.
                    await app.SyncAsync(actor, Wire.PushNoPull(actor,
                        Wire.TaskField(Guid.CreateVersion7(), actor, task, actor, "title", "mevcut", counter: 1)));
                    break;
                case "PreNullBaskasinin":
                    // BASKASI (digerActor) gorevi yaratir -- actor SAHIP DEGIL (bulgu 5'in kosulu).
                    var digerActor = Guid.NewGuid();
                    await app.SyncAsync(digerActor, Wire.PushNoPull(digerActor,
                        Wire.TaskField(Guid.CreateVersion7(), digerActor, task, digerActor, "title", "baskasinin", counter: 1)));
                    break;
                case "PreScopeUyeyim":
                case "PreScopeUyeDegilim":
                    var projectP = Guid.NewGuid();
                    var pSahibi = Guid.NewGuid();
                    await app.SyncAsync(pSahibi, Wire.PushNoPull(pSahibi, Wire.Op(Guid.CreateVersion7(), pSahibi, projectP, pSahibi, 1,
                        fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(pSahibi, 1)) },
                        entityType: "Project")));
                    if (preCase == "PreScopeUyeyim")
                    {
                        await app.SyncAsync(pSahibi, Wire.PushNoPull(pSahibi, Wire.Op(Guid.CreateVersion7(), pSahibi, projectP, pSahibi, 2,
                            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
                            {
                                ["members"] = new([new WireSetAdd(actor.ToString(), Guid.CreateVersion7(), Wire.Hlc(pSahibi, 2))], null),
                            },
                            entityType: "Project")));
                    }

                    // T'yi P'ye scope'lu YARATIR -- pSahibi yazar, boylece actor'in KENDI tasks.owner_id'si
                    // MATERYALIZE OLMAZ (IZIN_PRE'in scope kolunu, null kolunu DEGIL, sinamak icin onemli).
                    await app.SyncAsync(pSahibi, Wire.PushNoPull(pSahibi,
                        Wire.TaskField(Guid.CreateVersion7(), pSahibi, task, pSahibi, "projectId", projectP.ToString(), counter: 3)));
                    break;
            }
        }

        // --- postScope hedefi kurulumu ---
        string? postValue = postCase switch
        {
            "PostNull" => null,
            "PostSahibim" => await CreateOwnedProjectAsync(app, actor),
            "PostUyeDegilim" => await CreateOwnedProjectAsync(app, Guid.NewGuid()), // yabanci sahiplenir, actor UYE DEGIL
            _ => throw new ArgumentOutOfRangeException(nameof(postCase), postCase, null),
        };

        // --- ACT: actor, T'yi postValue scope'una YARATIR (Yeni) ya da TASIR (mevcut) ---
        // wallOffset: preScope kurulumunun "projectId" yazimiyla (varsa) AYNI wallMs'te LWW sayac-
        // esitligi kirilma tuzagina DUSMEMEK icin (bu oturumda H3/H4'un DAHA ONCE bulunan AYNI HLC
        // hatasi) -- ACT KESINLIKLE daha SONRAKI bir HLC tasir.
        var op = preCase == "Yeni"
            ? Wire.TaskFields(Guid.CreateVersion7(), actor, task, actor, 1, ("title", "yeni gorev"), ("projectId", postValue))
            : Wire.TaskField(Guid.CreateVersion7(), actor, task, actor, "projectId", postValue, counter: 10, wallOffset: 1000);

        var result = await app.SyncAsync(actor, Wire.PushNoPull(actor, op));
        result.Applied.ShouldHaveSingleItem().Code.ShouldBe(expectedCode, $"preCase={preCase} postCase={postCase}");
    }

    private static async Task<string> CreateOwnedProjectAsync(SyncTestApp app, Guid owner)
    {
        var project = Guid.NewGuid();
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("Q", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        return project.ToString();
    }
}
