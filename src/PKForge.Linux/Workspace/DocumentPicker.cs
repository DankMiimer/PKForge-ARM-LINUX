using PKForge.Application;
using PKForge.Domain;

namespace PKForge.Linux.Workspace;

public sealed class DocumentPicker(IFileDialogService dialogs) : IDocumentPicker
{
    public async ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default) => (await dialogs.PickAsync(new(FilePurpose.Save, "Open save"), cancellationToken)).FirstOrDefault();
    public async ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default) => await dialogs.PickAsync(new(FilePurpose.Pokemon, "Import Pokémon", true), cancellationToken);
}
