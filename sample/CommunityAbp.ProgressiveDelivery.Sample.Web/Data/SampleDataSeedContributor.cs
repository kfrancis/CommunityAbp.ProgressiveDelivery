using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Data;

/// <summary>
/// Seeds the tracks, rollouts, assignments and history shown in the design mockups. Idempotent.
/// </summary>
public class SampleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string PatientSearch = "Cab.Patients.Search";
    public const string PatientDuplicates = "Cab.Patients.Duplicates";
    public const string BillingStatements = "Cab.Billing.Statements";
    public const string DocumentsOcr = "Cab.Documents.Ocr";
    public const string PortalUpload = "Cab.Portal.Upload";
    public const string SchedulingSlots = "Cab.Scheduling.Slots";

    private readonly FeatureTrackManager _trackManager;
    private readonly FeatureAssignmentManager _assignmentManager;
    private readonly IFeatureTrackRepository _trackRepository;

    public SampleDataSeedContributor(FeatureTrackManager trackManager, FeatureAssignmentManager assignmentManager, IFeatureTrackRepository trackRepository)
    {
        _trackManager = trackManager;
        _assignmentManager = assignmentManager;
        _trackRepository = trackRepository;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (await _trackRepository.FindByNameAsync(PatientSearch, includeDetails: false) is not null)
        {
            return;
        }

        var search = await _trackManager.CreateAsync(PatientSearch, "Patient search engine", "How patient search resolves a query into a ranked result list.",
            levelZeroDescription: "Direct LIKE query against the patient table on surname, given name and date of birth.");
        await _trackManager.AddLevelAsync(search, 1, "Dedicated search index", "Search returns faster and no longer times out on large clinics. Results are otherwise identical.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(search, 2, "Fuzzy name matching", "Misspelled and phonetically similar names are found (\"Tomsen\" finds \"Thompson\"). Results are ordered by match rather than alphabetically.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(search, 3, "Identifier-aware ranking", "Health card, MRN and phone number are looked up in parallel with the name query and merged into one ranked list.", isPerformanceSensitive: true, fallbackPolicy: FallbackPolicy.Idempotent);
        await _trackManager.AddLevelAsync(search, 4, "Write-through index cache", "A patient registered seconds ago appears in search immediately instead of after the next index run.", fallbackPolicy: FallbackPolicy.DemoteOnly);
        await _trackManager.SetOfficialLevelAsync(search, 1, "Level 1 held 100.00 % for 30 days with no demotions; promoted to official.");
        await _trackManager.StartRolloutAsync(search, 2, 2_500);
        await _trackManager.StartRolloutAsync(search, 3, 500);
        await _trackManager.UpdateRolloutAsync(search, 3, status: FeatureRolloutStatus.Paused);

        var duplicates = await _trackManager.CreateAsync(PatientDuplicates, "Duplicate patient detection");
        await _trackManager.AddLevelAsync(duplicates, 1, "Probabilistic matching", "Near-duplicate patients are flagged on registration using demographic similarity.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(duplicates, 2, "Merge suggestions", "Suggested merges appear on the patient record with a confidence score.");
        await _trackManager.StartRolloutAsync(duplicates, 1, 5_000);

        var statements = await _trackManager.CreateAsync(BillingStatements, "Statement generation");
        await _trackManager.AddLevelAsync(statements, 1, "Templated PDF", "Statements use the new template engine; layout is identical.", fallbackPolicy: FallbackPolicy.Idempotent);
        await _trackManager.AddLevelAsync(statements, 2, "Batch rendering", "Monthly statement runs finish in minutes instead of hours.", fallbackPolicy: FallbackPolicy.Idempotent);
        await _trackManager.SetOfficialLevelAsync(statements, 2, "Level 2 promoted after 14 days at 100.00 %.");

        var ocr = await _trackManager.CreateAsync(DocumentsOcr, "Document OCR extraction");
        await _trackManager.AddLevelAsync(ocr, 1, "Layout-aware OCR", "Tables and columns in scanned referrals are extracted as structured fields.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(ocr, 2, "Handwriting model", "Handwritten notes are transcribed; accuracy is lower than printed text.", isPerformanceSensitive: true, fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(ocr, 3, "Auto-file to chart", "Extracted documents are filed to the patient chart without review.", fallbackPolicy: FallbackPolicy.None);
        await _trackManager.StartRolloutAsync(ocr, 1, 100);

        var upload = await _trackManager.CreateAsync(PortalUpload, "Portal upload widget", isEnabled: false);
        await _trackManager.AddLevelAsync(upload, 1, "Drag-and-drop uploader", "Patients can drop multiple files at once.");

        var slots = await _trackManager.CreateAsync(SchedulingSlots, "Slot availability engine");
        await _trackManager.AddLevelAsync(slots, 1, "Provider-aware availability", "Slots respect provider working hours.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(slots, 2, "Room constraints", "Slots also respect room availability.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(slots, 3, "Predictive overbooking", "Expected no-shows open extra capacity.", isPerformanceSensitive: true, fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.SetOfficialLevelAsync(slots, 3, "Promoted after pilot.");

        // Assignments and history that make the inspection and history pages interesting.
        var ops = FeatureSubject.User(SamplePersonas.OpsPersona.UserId);
        var support = FeatureSubject.User(SamplePersonas.SupportPersona.UserId);
        var pilotUser = FeatureSubject.User(new Guid("7c2a1f48-9b30-4d21-8e4b-2f0c9a771d55"));
        var mobileClient = FeatureSubject.Client("cab-mobile");

        await _assignmentManager.AssignAsync(search, ops, 3, FeatureTransitionType.ManualOverride, "Pilot user — search relevance. Requested by product.");
        await _assignmentManager.AssignAsync(search, pilotUser, 2, FeatureTransitionType.AutomaticPromotion, "Included in rollout cohort for level 2 at 25.00 %.");
        await _assignmentManager.AssignAsync(search, mobileClient, 0, FeatureTransitionType.ManualOverride, "Legacy client pinned to the original LIKE query.");
        await _assignmentManager.AssignAsync(search, support, 3, FeatureTransitionType.ManualOverride, "Support pilot");
        await _assignmentManager.AssignAsync(search, support, 2, FeatureTransitionType.AutomaticDemotion, "Level 3 failed with TimeoutException");
        await _assignmentManager.AssignAsync(slots, FeatureSubject.Tenant(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), 0, FeatureTransitionType.EmergencyOverride, "Incident INC-2288 — slot engine returning empty availability.");
        await _assignmentManager.AssignAsync(ocr, ops, 2, FeatureTransitionType.ManualOverride, "Evaluating handwriting model");

    }
}
