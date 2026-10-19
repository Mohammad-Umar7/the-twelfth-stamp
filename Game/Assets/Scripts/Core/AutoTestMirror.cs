using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    public partial class AutoTest
    {
        /// <summary>
        /// Home's bathroom mirror: stand at the basin facing the glass and prove the reflection is live and lit, and that it
        /// shows the player's own body (layer MirrorOnly) by comparing the reflection with that body left out and drawn.
        /// The player camera must never draw that body; no reflection may contain the first-person arms, 3D UI or glass.
        /// </summary>
        IEnumerator MirrorCheck(ApartmentCell apt)
        {
            var mirror = apt.GetComponentsInChildren<PlanarMirror>(true).FirstOrDefault();
            Check(mirror != null && mirror.Glass != null, "home's bathroom mirror is a real mirror (PlanarMirror on the vanity glass)");
            if (mirror == null || mirror.Glass == null) yield break;

            var feetBefore = Game.Rig.Feet;
            float yawBefore = Game.Rig.Head.eulerAngles.y;
            var facing = mirror.Glass.bounds.center - apt.BathroomInside.position; // look into the glass
            facing.y = 0f;
            facing.Normalize();
            Game.Rig.PlaceAt(apt.BathroomInside.position, Quaternion.LookRotation(facing).eulerAngles.y);
            Hud.I?.HideTitleCard(); // the arrival card is a screen overlay; keep it out of the evidence shots
            yield return WaitUntil(() => mirror.Live && mirror.RenderedFrame >= Time.frameCount - 1, 4f, "standing at the basin, the mirror draws its reflection");
            yield return new WaitForSeconds(0.6f);

            var rc = mirror.ReflectionCamera(0);
            const int never = (1 << Layers.PlayerBody) | (1 << Layers.UI3D) | (1 << Layers.MirrorGlass);
            Check(rc != null && (rc.cullingMask & (1 << Layers.MirrorOnly)) != 0 && (rc.cullingMask & never) == 0,
                "reflections include the player's body and never the first-person arms, the 3D UI or mirror glass");
            Check((Game.Rig.Cam.cullingMask & (1 << Layers.MirrorOnly)) == 0, "the player camera never draws the mirror body (no looking out through your own head)");

            var body = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.layer == Layers.MirrorOnly).ToList();
            Check(body.Count > 0, "the player has a full body for mirrors (" + body.Count + " renderers on the MirrorOnly layer)");
            Check(MirrorAvatar.I != null && MirrorAvatar.I.UsingModel, "the mirror body is Saeed, the Blender model (Saeed.fbx), not the stand-in");
            bool framed = rc != null && body.Any(r =>
            {
                var vp = rc.WorldToViewportPoint(r.bounds.center);
                return vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            });
            Check(framed, "the body stands inside the reflected view");

            Color[] drawn = null, left = null;
            yield return Capture(mirror.ReflectionTexture(0), c => drawn = c);
            PlanarMirror.TestHideBody = true;
            yield return null;
            yield return null;
            yield return Capture(mirror.ReflectionTexture(0), c => left = c);
            PlanarMirror.TestHideBody = false;
            // Only the part of the reflection that shows through the glass counts: the glass's screen rectangle, mirrored
            // in x like the reflection itself.
            var view = Game.Rig.Cam;
            var gb = mirror.Glass.bounds;
            float x0 = 1f, x1 = 0f, y0 = 1f, y1 = 0f;
            for (int c = 0; c < 8; c++)
            {
                var corner = gb.center + Vector3.Scale(gb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                var vp = view.WorldToViewportPoint(corner);
                x0 = Mathf.Min(x0, 1f - vp.x); x1 = Mathf.Max(x1, 1f - vp.x);
                y0 = Mathf.Min(y0, vp.y); y1 = Mathf.Max(y1, vp.y);
            }
            float lum = 0f, diff = 0f;
            int changed = 0, inside = 0;
            if (drawn != null && left != null && drawn.Length == left.Length && drawn.Length == CaptureW * CaptureH)
            {
                for (int i = 0; i < drawn.Length; i++)
                {
                    float u = (i % CaptureW + 0.5f) / CaptureW, v = (i / CaptureW + 0.5f) / CaptureH;
                    if (u < x0 || u > x1 || v < y0 || v > y1) continue;
                    inside++;
                    lum += drawn[i].r * 0.299f + drawn[i].g * 0.587f + drawn[i].b * 0.114f;
                    float d = (Mathf.Abs(drawn[i].r - left[i].r) + Mathf.Abs(drawn[i].g - left[i].g) + Mathf.Abs(drawn[i].b - left[i].b)) / 3f;
                    diff += d;
                    if (d > 0.01f) changed++;
                }
                if (inside > 0) { lum /= inside; diff /= inside; }
            }
            float share = inside > 0 ? changed / (float)inside : 0f;
            Note("glass on screen: x " + x0.ToString("0.00") + "-" + x1.ToString("0.00") + ", y " + y0.ToString("0.00") + "-" + y1.ToString("0.00") + " (" + inside + " samples)");
            Check(inside > 40 && lum > 0.004f, "the reflection in the glass is lit, not black (mean linear luminance " + lum.ToString("0.0000") + ")");
            Check(share > 0.08f, "the player's body shows in the mirror (" + (share * 100f).ToString("0.0") + "% of what the glass shows changes when the body is left out, mean " + diff.ToString("0.0000") + ")");

            // His face is lit in the glass, not a dark shape: the reflection around the face, measured in the capture.
            var headBone = MirrorAvatar.I != null ? MirrorAvatar.I.HeadBone : null;
            if (headBone != null && rc != null && drawn != null && drawn.Length == CaptureW * CaptureH)
            {
                var look = Vector3.ProjectOnPlane(Game.Rig.Head.forward, Vector3.up).normalized;
                var face = headBone.position + Vector3.up * 0.09f + look * 0.09f;
                var fvp = rc.WorldToViewportPoint(face);
                float faceLum = 0f;
                int n = 0;
                int cx = Mathf.RoundToInt(fvp.x * CaptureW - 0.5f), cy = Mathf.RoundToInt(fvp.y * CaptureH - 0.5f);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= CaptureW || y >= CaptureH) continue;
                        var c = drawn[y * CaptureW + x];
                        faceLum += c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                        n++;
                    }
                faceLum = n > 0 ? faceLum / n : 0f;
                Check(fvp.z > 0f && n > 0 && faceLum > 0.03f, "his face is lit in the mirror, not a dark shape (linear luminance " + faceLum.ToString("0.000") + ")");
            }
            yield return Shot("20b_bathroom_mirror");

            // A step to the side and a turn: the reflection follows the eye.
            Game.Rig.PlaceAt(apt.BathroomInside.position + Vector3.Cross(Vector3.up, facing).normalized * 0.45f, Quaternion.LookRotation(facing).eulerAngles.y - 18f);
            yield return new WaitForSeconds(0.5f);
            Check(mirror.Live && mirror.RenderedFrame >= Time.frameCount - 2, "the reflection keeps up when you move and turn");
            yield return Shot("20c_bathroom_mirror_angle");

            Game.Rig.PlaceAt(apt.BathroomInside.position, Quaternion.LookRotation(-facing).eulerAngles.y);
            yield return new WaitForSeconds(0.5f);
            Check(!mirror.Live, "turning your back on the mirror stops it drawing (no cost when it is not seen)");
            Game.Rig.PlaceAt(feetBefore, yawBefore);
            yield return new WaitForSeconds(0.5f);
        }

        const int CaptureW = 160, CaptureH = 90;

        /// <summary>Reads a (possibly multisampled HDR) render texture back at low resolution, at the end of the frame.</summary>
        IEnumerator Capture(RenderTexture rt, Action<Color[]> done)
        {
            yield return new WaitForEndOfFrame();
            if (rt == null || !rt.IsCreated()) { done(null); yield break; }
            const int w = CaptureW, h = CaptureH;
            var small = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(rt, small);
            var previous = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            done(tex.GetPixels());
            Destroy(tex);
        }
    }
}
