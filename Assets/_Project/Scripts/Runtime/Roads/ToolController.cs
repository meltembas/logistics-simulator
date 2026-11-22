using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Logistics
{
    public enum Tool { Road, Roundabout, Highway }

    /// <summary>Mouse construction: drag to draw roads (bridges auto-complete over water), right-drag to erase,
    /// click to place roundabouts, click-click to span a highway. Shows a ghost tile under the cursor.</summary>
    public class ToolController : MonoBehaviour
    {
        public GameManager game;
        public Tool Current { get; private set; } = Tool.Road;
        public event Action<Tool> ToolChanged;
        public event Action<string, bool> Message;
        public event Action<bool> Built; // true = placed, false = removed

        TileCoord? dragLast, lastPainted, lastErased, highwayStart;
        bool painting, erasing;
        float messageCooldown;
        GameObject ghost, highwayGhost;
        MeshRenderer ghostR, highwayR;
        MeshFilter highwayMf;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        World W => game.World;

        public void Init(MaterialLibrary lib)
        {
            mpb = new MaterialPropertyBlock();
            ghost = new GameObject("Ghost");
            ghost.transform.SetParent(transform, false);
            var mb = new MeshBuilder(1);
            float h = 3.85f;
            mb.Flat(0, -h, -h, h, h, 0f, 1f);
            ghost.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Ghost", false);
            ghostR = ghost.AddComponent<MeshRenderer>();
            ghostR.sharedMaterial = lib.ghost;
            ghostR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghost.SetActive(false);

            highwayGhost = new GameObject("HighwayGhost");
            highwayGhost.transform.SetParent(transform, false);
            highwayMf = highwayGhost.AddComponent<MeshFilter>();
            highwayR = highwayGhost.AddComponent<MeshRenderer>();
            highwayR.sharedMaterial = lib.ghost;
            highwayR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highwayGhost.SetActive(false);
        }

        public void SetTool(Tool t)
        {
            Current = t;
            highwayStart = null;
            ToolChanged?.Invoke(t);
            if (t == Tool.Highway) Say("Otoyol: başlangıç yolunu, sonra bitiş yolunu tıkla", false);
            if (t == Tool.Roundabout) Say("Göbek kavşak: bir yol karosuna tıkla", false);
        }

        void Say(string msg, bool danger)
        {
            if (string.IsNullOrEmpty(msg) || messageCooldown > 0f) return;
            messageCooldown = 0.6f;
            Message?.Invoke(msg, danger);
        }

        bool RaycastTile(Vector2 screen, out TileCoord tile)
        {
            tile = default;
            var cam = game.mainCamera;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return false;
            tile = W.Grid.WorldToTile(ray.GetPoint(d));
            return W.Grid.InMap(tile);
        }

        void Update()
        {
            messageCooldown -= Time.unscaledDeltaTime;
            bool active = game != null && game.World != null && game.State == GameState.Playing;
            var mouse = Mouse.current;
            if (!active || mouse == null)
            {
                painting = erasing = false;
                if (ghost != null) ghost.SetActive(false);
                if (highwayGhost != null) highwayGhost.SetActive(false);
                return;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) SetTool(Tool.Road);
                if (kb.tKey.wasPressedThisFrame) SetTool(Tool.Roundabout);
                if (kb.hKey.wasPressedThisFrame) SetTool(Tool.Highway);
            }

            var pos = mouse.position.ReadValue();
            bool overUI = game.ui != null && game.ui.IsPointerOverUI(pos);
            if (!mouse.leftButton.isPressed) { painting = false; dragLast = null; lastPainted = null; }
            if (!mouse.rightButton.isPressed) { erasing = false; lastErased = null; }

            if (overUI && !painting && !erasing)
            {
                ghost.SetActive(false);
                highwayGhost.SetActive(false);
                return;
            }
            if (!RaycastTile(pos, out var tile))
            {
                ghost.SetActive(false);
                return;
            }

            UpdateGhost(tile);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (highwayStart.HasValue) { highwayStart = null; highwayGhost.SetActive(false); }
                erasing = true;
            }
            if (erasing && (!lastErased.HasValue || lastErased.Value != tile))
            {
                lastErased = tile;
                var r = W.Editor.Erase(tile);
                if (r == EditResult.Ok) Built?.Invoke(false);
                else if (r == EditResult.InUse || r == EditResult.Fixed) Say(RoadEditor.Describe(r), true);
            }

            switch (Current)
            {
                case Tool.Road: HandleRoad(mouse, tile); break;
                case Tool.Roundabout:
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var r = W.Editor.PlaceRoundabout(tile);
                        if (r == EditResult.Ok) { Built?.Invoke(true); if (W.Inventory.Roundabouts == 0) SetTool(Tool.Road); }
                        else Say(RoadEditor.Describe(r), true);
                    }
                    break;
                case Tool.Highway: HandleHighway(mouse, tile); break;
            }
        }

        void HandleRoad(Mouse mouse, TileCoord tile)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                painting = true;
                dragLast = null;
                lastPainted = null;
            }
            if (!painting) return;
            if (lastPainted.HasValue && lastPainted.Value == tile) return;

            // Walk a 4-connected line from the last painted tile so fast drags don't skip.
            var from = lastPainted ?? tile;
            var cur = from;
            if (!lastPainted.HasValue) Paint(cur);
            int guard = 0;
            while (cur != tile && guard++ < 64)
            {
                int dx = tile.x - cur.x, dy = tile.y - cur.y;
                if (Mathf.Abs(dx) >= Mathf.Abs(dy)) cur = new TileCoord(cur.x + Math.Sign(dx), cur.y);
                else cur = new TileCoord(cur.x, cur.y + Math.Sign(dy));
                Paint(cur);
            }
            lastPainted = tile;
        }

        void Paint(TileCoord t)
        {
            bool wasRoad = W.Roads.IsRoad(t);
            var r = W.Editor.PaintStep(dragLast, t, out dragLast);
            if (r == EditResult.Ok)
            {
                if (!wasRoad || dragLast.HasValue) Built?.Invoke(true);
                // A bridge jump lands on the far bank; continue the drag from there.
                if (dragLast.HasValue && dragLast.Value != t) lastPainted = dragLast;
            }
            else if (r == EditResult.NoRoads || r == EditResult.NoBridges || r == EditResult.BridgeTooLong || r == EditResult.OutOfBounds)
                Say(RoadEditor.Describe(r), true);
        }

        void HandleHighway(Mouse mouse, TileCoord tile)
        {
            if (highwayStart.HasValue)
            {
                var a = W.Grid.TileCenter(highwayStart.Value);
                var b = W.Grid.TileCenter(tile);
                var ok = W.Editor.CanPlaceHighway(highwayStart.Value, tile) == EditResult.Ok;
                DrawHighwayGhost(a, b, ok);
            }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (!highwayStart.HasValue)
            {
                if (W.Inventory.Highways <= 0) { Say(RoadEditor.Describe(EditResult.NoHighways), true); return; }
                if (!W.Roads.IsRoad(tile) || W.Roads.IsBridge(tile)) { Say(RoadEditor.Describe(EditResult.NotRoad), true); return; }
                highwayStart = tile;
                return;
            }
            var r = W.Editor.PlaceHighway(highwayStart.Value, tile);
            if (r == EditResult.Ok)
            {
                Built?.Invoke(true);
                highwayStart = null;
                highwayGhost.SetActive(false);
                if (W.Inventory.Highways == 0) SetTool(Tool.Road);
            }
            else Say(RoadEditor.Describe(r), true);
        }

        void DrawHighwayGhost(Vector3 a, Vector3 b, bool ok)
        {
            highwayGhost.SetActive(true);
            RouteBuilder.HighwayControls(a, b, out var p1, out var p2);
            var mb = new MeshBuilder(1);
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                var c0 = RouteBuilder.Bezier(a, p1, p2, b, i / (float)n) + Vector3.up * 0.3f;
                var c1 = RouteBuilder.Bezier(a, p1, p2, b, (i + 1) / (float)n) + Vector3.up * 0.3f;
                var t = c1 - c0; t.y = 0f;
                var r = RouteBuilder.Right(t.normalized) * 3.4f;
                mb.Quad(0, c0 - r, c1 - r, c1 + r, c0 + r, 1f);
            }
            if (highwayMf.sharedMesh != null) Destroy(highwayMf.sharedMesh);
            highwayMf.sharedMesh = mb.ToMesh("HighwayGhost", false);
            highwayR.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, ok ? Palette.GhostOk : Palette.GhostBad);
            highwayR.SetPropertyBlock(mpb);
        }

        void UpdateGhost(TileCoord tile)
        {
            ghost.SetActive(true);
            var c = W.Grid.TileCenter(tile);
            bool ok;
            switch (Current)
            {
                case Tool.Roundabout: ok = W.Roads.IsRoad(tile) && !W.Roads.IsBridge(tile) && W.Inventory.Roundabouts > 0; break;
                case Tool.Highway: ok = W.Roads.IsRoad(tile) && !W.Roads.IsBridge(tile); break;
                default:
                    ok = W.Roads.IsRoad(tile) || (W.Grid.IsBuildableGround(tile) && W.Inventory.Roads > 0)
                         || (W.Grid.IsWater(tile) && W.Grid.InActive(tile) && W.Inventory.Bridges > 0);
                    break;
            }
            if (erasing) ok = W.Roads.IsRoad(tile) && !W.Roads.IsFixed(tile);
            float y = W.Grid.IsWater(tile) && !W.Roads.IsRoad(tile) ? TerrainView.WaterLevel + 0.05f : 0.42f;
            ghost.transform.position = new Vector3(c.x, y, c.z);
            ghostR.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, ok ? Palette.GhostOk : Palette.GhostBad);
            ghostR.SetPropertyBlock(mpb);
        }
    }
}
