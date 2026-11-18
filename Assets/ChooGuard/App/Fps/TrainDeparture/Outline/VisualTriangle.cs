// Port of Godot Geometry3D::ray_intersects_triangle, geometry_3d.h:44-82.
// ed1daf0bf001b61586d9930840f2f1394092c079; MIT, full notice in ThirdParty/Godot-LICENSE.txt.
// Copyright (c) 2014-present Godot Engine contributors.
// Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur.
// Modifications: expose t/u/v/determinant; relative parallel tolerance for differently scaled source meshes.
using UnityEngine;
namespace ChooGuard.App.Fps.TrainDeparture
{
    public static class VisualTriangle
    {
        public static bool Hit(Vector3 origin, Vector3 direction, Vector3 v0, Vector3 v1, Vector3 v2,
            out float t, out float u, out float v, out float determinant)
        {
            t = u = v = 0;
            Vector3 e1 = v1 - v0, e2 = v2 - v0;
            Vector3 h = Vector3.Cross(direction, e2);
            float a = Vector3.Dot(e1, h); determinant = a;
            // Relative tolerance preserves tiny source triangles and unnormalised transformed rays.
            float scale = Mathf.Sqrt(e1.sqrMagnitude * h.sqrMagnitude);
            if (scale == 0 || Mathf.Abs(a) <= 1e-7f * scale) return false;
            float f = 1.0f / a;
            Vector3 s = origin - v0;
            u = f * Vector3.Dot(s, h);
            if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1);
            v = f * Vector3.Dot(direction, q);
            if (v < 0 || u + v > 1) return false;
            t = f * Vector3.Dot(e2, q);
            return t > .00001f;
        }
    }
}
