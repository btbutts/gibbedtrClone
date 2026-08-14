using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public interface IFilePickerService
    {
        Task<IReadOnlyList<string>> OpenFilesAsync(
            TopLevel? owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, bool allowMultiple);

        Task<string?> SaveFileAsync(
            TopLevel? owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, string? suggestedFileName);
    }
}
