using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Baryonyx.Health.Editor
{
    public sealed class HealthScreenBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            const string testPrefab =
                "Assets/Baryonyx/Features/Health/Tests/PlayMode/Resources/BaryonyxHealthScreenTest.prefab";
            if (
                (report.summary.options & BuildOptions.IncludeTestAssemblies) == 0
                && File.Exists(testPrefab)
            )
                throw new BuildFailedException(
                    "Health screen test resources remain. Finish test cleanup before a production build."
                );
        }
    }
}
