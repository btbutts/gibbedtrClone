using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Gibbed.DeusEx3.DRMEdit.Services
{
    public class AvaloniaFilePickerService : IFilePickerService
    {
        public async Task<IReadOnlyList<string>> OpenFilesAsync(
            TopLevel? owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, bool allowMultiple)
        {
            if (owner == null)
            {
                return System.Array.Empty<string>();
            }

            var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                FileTypeFilter = fileTypes,
                AllowMultiple = allowMultiple,
            });

            return result
                .Select(f => f.TryGetLocalPath())
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();
        }

        public async Task<string?> SaveFileAsync(
            TopLevel? owner, string title, IReadOnlyList<FilePickerFileType> fileTypes, string? suggestedFileName)
        {
            if (owner == null)
            {
                return null;
            }

            var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                FileTypeChoices = fileTypes,
                SuggestedFileName = suggestedFileName,
            });

            return result?.TryGetLocalPath();
        }
    }
}
