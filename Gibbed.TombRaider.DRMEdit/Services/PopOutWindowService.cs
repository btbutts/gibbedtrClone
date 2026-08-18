using System;
using System.Collections.Generic;
using Gibbed.TombRaider.DRMEdit.ViewModels;
using Gibbed.TombRaider.DRMEdit.Views;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public class PopOutWindowService : IPopOutWindowService
    {
        private sealed class Entry
        {
            public required PopOutWindow Window { get; init; }
            public bool ClosedProgrammatically { get; set; }
        }

        private readonly Dictionary<DocumentTabViewModel, Entry> _entries = new();

        public void PopOut(DocumentTabViewModel document, Action onClosedByUser)
        {
            var window = new PopOutWindow { DataContext = document };
            var entry = new Entry { Window = window };
            _entries[document] = entry;

            window.Closed += (_, _) =>
            {
                _entries.Remove(document);
                if (entry.ClosedProgrammatically == false)
                {
                    onClosedByUser();
                }
            };

            document.GetTopLevel = () => window;

            window.Show();
        }

        public void Close(DocumentTabViewModel document)
        {
            if (_entries.TryGetValue(document, out var entry) == false)
            {
                return;
            }

            entry.ClosedProgrammatically = true;
            entry.Window.Close();
        }

        public IReadOnlyCollection<DocumentTabViewModel> GetOpenDocuments()
        {
            return new List<DocumentTabViewModel>(_entries.Keys);
        }
    }
}
