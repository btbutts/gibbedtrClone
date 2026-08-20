using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param == null)
            {
                return null;
            }

            var name = param.GetType().FullName!.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
            if (name.EndsWith("ViewModel", StringComparison.Ordinal) == true)
            {
                name = name.Substring(0, name.Length - "ViewModel".Length) + "View";
            }
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
