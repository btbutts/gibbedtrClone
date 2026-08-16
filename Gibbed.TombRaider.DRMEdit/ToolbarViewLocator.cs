using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit
{
    public class ToolbarViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param == null)
            {
                return null;
            }

            var fullName = param.GetType().FullName!;
            var name = fullName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
            if (name.EndsWith("ViewModel", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "ViewModel".Length) + "Toolbar";
            }
            var type = Type.GetType(name);

            return type != null ? (Control)Activator.CreateInstance(type)! : null;
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
