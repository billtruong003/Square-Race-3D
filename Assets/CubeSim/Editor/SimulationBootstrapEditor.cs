using UnityEditor;
using UnityEngine;
using CubeSim.Core;

namespace CubeSim.EditorTools
{
    /// <summary>Play-mode controls so a run can be restarted or reseeded without editing anything.</summary>
    [CustomEditor(typeof(SimulationBootstrap))]
    public class SimulationBootstrapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var bootstrap = (SimulationBootstrap)target;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.LabelField("Run Controls", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Restart")) bootstrap.Build();
                    if (GUILayout.Button("Next Seed")) bootstrap.RestartWithNextSeed();
                    if (GUILayout.Button("Random Seed")) bootstrap.RestartWithRandomSeed();
                }

                SimulationRunner runner = bootstrap.Runner;
                if (runner != null)
                {
                    int bounces = 0;
                    int armed = 0;
                    for (int i = 0; i < runner.Racers.Length; i++)
                    {
                        bounces += runner.Racers[i].BounceCount;
                        if (runner.Racers[i].Armed) armed++;
                    }

                    EditorGUILayout.LabelField("Elapsed", runner.ElapsedTime.ToString("F1") + " s");
                    EditorGUILayout.LabelField("Alive", runner.AliveCount + " / " + runner.RacerCount);
                    EditorGUILayout.LabelField("Armed", armed.ToString());
                    EditorGUILayout.LabelField("Crush deaths", runner.CrushDeaths.ToString());
                    EditorGUILayout.LabelField("Total bounces", bounces.ToString());
                    EditorGUILayout.LabelField("Result", runner.Result.ToString());
                    Repaint();
                }
            }
        }
    }
}
