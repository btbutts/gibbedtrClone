using System;
using System.Collections.Generic;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Services
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
