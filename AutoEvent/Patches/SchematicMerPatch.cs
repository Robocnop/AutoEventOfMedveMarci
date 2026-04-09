using System.Diagnostics;
using System.IO;
using HarmonyLib;

namespace AutoEvent.Patches;

public static class SchematicMerPatch
{
    public static bool Prefix(ref string __result)
    {
        var stackTrace = new StackTrace();
        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame.GetMethod().DeclaringType;
            var assemblyName = declaringType.Assembly.GetName().Name;

            if (assemblyName.Contains("AutoEvent") && declaringType.Name == "Extensions")
            {
                __result = Path.Combine(AutoEvent.BaseConfigPath, "Schematics", "ProjectMER");
                return false;
            }
        }

        return true;
    }

    internal static void ApplyPatch(Harmony harmony)
    {
        var targetMethod = AccessTools.PropertyGetter(
            typeof(ProjectMER.ProjectMER), nameof(ProjectMER.ProjectMER.SchematicsDir));
        var prefixMethod = AccessTools.Method(typeof(SchematicMerPatch), nameof(Prefix));
        harmony.Patch(targetMethod, new HarmonyMethod(prefixMethod));
    }
}