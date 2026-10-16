using Ged.Application.Common;
using Ged.Application.UseCases;
using Ged.Domain.ValueObjects;
using Ged.Infrastructure.Services;
using Ged.Infrastructure.Sql;
using Ged.Infrastructure.Storage;

namespace Ged.Bootstrap;

/// <summary>
/// Partagé par la CLI et le site web. Seul endroit qui connaît les implémentations concrètes : on y « branche » les adaptateurs sur les ports.
/// Injection de dépendances faite à la main, sans framework.
/// </summary>
public sealed class CompositionRoot
{
    public UploadDocument UploadDocument { get; }
    public AddVersion AddVersion { get; }
    public DownloadDocument DownloadDocument { get; }
    public GetDocument GetDocument { get; }
    public TagDocument TagDocument { get; }
    public RenameDocument RenameDocument { get; }
    public MoveDocument MoveDocument { get; }
    public SearchDocuments SearchDocuments { get; }
    public TrashDocument TrashDocument { get; }
    public RestoreDocument RestoreDocument { get; }
    public PurgeTrash PurgeTrash { get; }
    public CreateFolder CreateFolder { get; }
    public ListFolders ListFolders { get; }
    public DeleteFolder DeleteFolder { get; }
    public DocumentPolicy Policy { get; }

    public CompositionRoot(AppConfig config)
    {
        var policy = Policy = new DocumentPolicy(config.MaxFileSizeMb * 1024L * 1024L, config.TrashRetentionDays);

        // Adaptateurs : changer de base ne touche que la configuration lue ici.
        var connections = new SqlConnectionFactory(config.Database);
        new SchemaInitializer(connections).EnsureCreated();
        var documents = new SqlDocumentRepository(connections);
        var folders = new SqlFolderRepository(connections);
        var storage = new LocalDiskFileStorage(config.StoragePath);
        var clock = new SystemClock();
        var intake = new FileIntake(storage, new Sha256ChecksumCalculator(), policy);

        UploadDocument = new UploadDocument(documents, folders, storage, intake, clock);
        AddVersion = new AddVersion(documents, storage, intake, clock);
        DownloadDocument = new DownloadDocument(documents, storage);
        GetDocument = new GetDocument(documents);
        TagDocument = new TagDocument(documents);
        RenameDocument = new RenameDocument(documents);
        MoveDocument = new MoveDocument(documents, folders);
        SearchDocuments = new SearchDocuments(documents);
        TrashDocument = new TrashDocument(documents, clock);
        RestoreDocument = new RestoreDocument(documents);
        PurgeTrash = new PurgeTrash(documents, storage, clock, policy);
        CreateFolder = new CreateFolder(folders, clock);
        ListFolders = new ListFolders(folders);
        DeleteFolder = new DeleteFolder(folders, documents);
    }
}
