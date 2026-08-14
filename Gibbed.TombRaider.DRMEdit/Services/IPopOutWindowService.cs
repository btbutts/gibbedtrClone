using System;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Services
{
    public interface IPopOutWindowService
    {
        void PopOut(DocumentTabViewModel document, Action onClosedByUser);
        void Close(DocumentTabViewModel document);
    }
}
