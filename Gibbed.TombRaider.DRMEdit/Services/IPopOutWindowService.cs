using System;
using System.Collections.Generic;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public interface IPopOutWindowService
    {
        void PopOut(DocumentTabViewModel document, Action onClosedByUser);
        void Close(DocumentTabViewModel document);

        // Snapshot, not a live view, callers close each entry through document.Close(),
        // which mutates the service's internal state mid-enumeration.
        IReadOnlyCollection<DocumentTabViewModel> GetOpenDocuments();
    }
}
