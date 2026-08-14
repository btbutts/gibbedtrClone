using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public class SectionNode
    {
        public string DisplayName { get; }
        public string IconKey { get; }
        public DRM.Section Section { get; }

        public SectionNode(DRM.Section section)
        {
            Section = section;

            var typeName = section.Type.ToString();
            var name = section.Id.ToString("X8");
            name += " : " + typeName;
            name += string.Format(
                " [{0:X2} {1:X2} {2:X4} {3:X8}]",
                section.Flags, section.Unknown05, section.Unknown06, section.Unknown10);

            if (section.Data != null)
            {
                name += " (" + section.Data.Length.ToString() + ")";
            }

            DisplayName = name;
            IconKey = typeName;
        }
    }
}
