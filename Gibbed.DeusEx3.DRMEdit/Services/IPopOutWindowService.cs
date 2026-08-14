using System;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Services
{
    public interface IPopOutWindowService
    {
        void PopOut(DocumentTabViewModel document, Action onClosedByUser);
        void Close(DocumentTabViewModel document);
    }
}
