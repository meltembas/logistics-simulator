#if UNITY_EDITOR || DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Logistics
{
    /// <summary>
    /// Verification helper: queues real Input System mouse events (so ToolController, UI hit-testing and
    /// the camera all see genuine input) to drag across world tiles or click screen points.
    /// </summary>
    public class DebugInput : MonoBehaviour
    {
        public static string LastResult = "";

        static DebugInput Get()
        {
            var gm = GameManager.Instance;
            return gm.GetComponent<DebugInput>() ?? gm.gameObject.AddComponent<DebugInput>();
        }

        public static Vector2 TileToScreen(TileCoord t)
        {
            var gm = GameManager.Instance;
            var p = gm.mainCamera.WorldToScreenPoint(gm.World.Grid.TileCenter(t));
            return new Vector2(p.x, p.y);
        }

        /// <summary>Press at the first tile, move through the rest, release. button: 0 = left, 1 = right.</summary>
        public static void DragTiles(IList<TileCoord> tiles, int button = 0)
        {
            var pts = new List<Vector2>();
            foreach (var t in tiles) pts.Add(TileToScreen(t));
            Get().StartCoroutine(Get().Run(pts, button));
        }

        public static void Click(Vector2 screen, int button = 0) =>
            Get().StartCoroutine(Get().Run(new List<Vector2> { screen }, button));

        IEnumerator Run(List<Vector2> pts, int button)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            ushort mask = (ushort)(1 << button);
            Queue(mouse, pts[0], 0);
            yield return null;
            yield return null;
            Queue(mouse, pts[0], mask);
            yield return null;
            yield return null;
            for (int i = 1; i < pts.Count; i++)
            {
                // a few intermediate steps per tile so the drag looks like a hand
                var a = pts[i - 1];
                var b = pts[i];
                for (int k = 1; k <= 3; k++)
                {
                    Queue(mouse, Vector2.Lerp(a, b, k / 3f), mask);
                    yield return null;
                }
            }
            yield return null;
            Queue(mouse, pts[pts.Count - 1], 0);
            yield return null;
            yield return null;
            LastResult = $"done {pts.Count} points";
        }

        static void Queue(Mouse mouse, Vector2 pos, ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = buttons });
        }
    }
}
#endif
