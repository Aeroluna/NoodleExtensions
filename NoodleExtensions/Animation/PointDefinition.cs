namespace NoodleExtensions.Animation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using static NoodleExtensions.Plugin;

    public class PointDefinition
    {
        private List<PointData> _points;

        public static PointDefinition DynamicToPointData(dynamic dyn)
        {
            IEnumerable<List<object>> points = ((IEnumerable<object>)dyn)
                        ?.Cast<List<object>>();
            if (points == null)
            {
                return null;
            }

            PointDefinition pointData = new PointDefinition();
            foreach (List<object> rawPoint in points)
            {
                int flagIndex = -1;
                int cachedCount = rawPoint.Count;
                for (int i = cachedCount - 1; i > 0; i--)
                {
                    if (rawPoint[i] is string)
                    {
                        flagIndex = i;
                    }
                    else
                    {
                        break;
                    }
                }

                Functions easing = Functions.easeLinear;
                Splines spline = Splines.splineNone;
                List<object> copiedList = rawPoint.ToList();
                if (flagIndex != -1)
                {
                    List<string> flags = rawPoint.GetRange(flagIndex, cachedCount - flagIndex).Cast<string>().ToList();
                    copiedList.RemoveRange(flagIndex, cachedCount - flagIndex);

                    string easingString = flags.Where(n => n.StartsWith("ease")).FirstOrDefault();
                    if (easingString != null)
                    {
                        easing = (Functions)Enum.Parse(typeof(Functions), easingString);
                    }

                    // TODO: add more spicy splines
                    string splineString = flags.Where(n => n.StartsWith("spline")).FirstOrDefault();
                    if (splineString != null)
                    {
                        spline = (Splines)Enum.Parse(typeof(Splines), splineString);
                    }
                }

                if (copiedList.Count() == 2)
                {
                    Vector2 vector = new Vector2(Convert.ToSingle(copiedList[0]), Convert.ToSingle(copiedList[1]));
                    pointData.Add(new PointData(vector, easing, spline));
                }
                else if (copiedList.Count() == 4)
                {
                    Vector4 vector = new Vector4(Convert.ToSingle(copiedList[0]), Convert.ToSingle(copiedList[1]), Convert.ToSingle(copiedList[2]), Convert.ToSingle(copiedList[3]));
                    pointData.Add(new PointData(vector, easing, spline));
                }
                else
                {
                    Vector5 vector = new Vector5(Convert.ToSingle(copiedList[0]), Convert.ToSingle(copiedList[1]), Convert.ToSingle(copiedList[2]), Convert.ToSingle(copiedList[3]), Convert.ToSingle(copiedList[4]));
                    pointData.Add(new PointData(vector, easing, spline));
                }
            }

            return pointData;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder("{ ");
            if (_points != null)
            {
                _points.ForEach(n => stringBuilder.Append($"{n.Point.ToString()} "));
            }

            stringBuilder.Append("}");
            return stringBuilder.ToString();
        }

        public Vector3 Interpolate(float time)
        {
            if (_points == null || _points.Count == 0)
            {
                return _vectorZero;
            }

            if (_points.First().Point.w >= time)
            {
                return _points.First().Point;
            }

            if (_points.Last().Point.w <= time)
            {
                return _points.Last().Point;
            }

            SearchIndex(time, PropertyType.Vector3, out int l, out int r);

            float normalTime = (time - _points[l].Point.w) / (_points[r].Point.w - _points[l].Point.w);
            if (_points[r].Spline == Splines.splineCatmullRom)
            {
                return SmoothVectorLerp(_points, l, r, normalTime);
            }
            else
            {
                Vector3[] bp = new Vector3[r-l+1];
                float[] bt = new float[r-l+1];
                int count = 0;
                for(int j = l; j <= r; ++j)
                {
                    bp[count] = _points[j].Point;
                    bt[count++] = Easings.Interpolate(normalTime, _points[j].Easing);
                }

                while(--count > 0)
                {
                    for(int j = 0; j < count; ++j)
                    {
                        bp[j] = Vector3.LerpUnclamped(bp[j], bp[j+1], bt[j+1]);
                        bt[j] = (bt[j] + bt[j+1]) / 2;
                    }
                }
                return bp[0];
            }
        }

        public Quaternion InterpolateQuaternion(float time)
        {
            if (_points == null || _points.Count == 0)
            {
                return _quaternionIdentity;
            }

            if (_points.First().Point.w >= time)
            {
                return Quaternion.Euler(_points.First().Point);
            }

            if (_points.Last().Point.w <= time)
            {
                return Quaternion.Euler(_points.Last().Point);
            }

            SearchIndex(time, PropertyType.Quaternion, out int l, out int r);

            float normalTime = (time - _points[l].Point.w) / (_points[r].Point.w - _points[l].Point.w);
            Quaternion[] bp = new Quaternion[r-l+1];
            float[] bt = new float[r-l+1];
            int count = 0;
            for(int j = l; j <= r; ++j)
            {
                bp[count] = Quaternion.Euler(_points[j].Point);
                bt[count++] = Easings.Interpolate(normalTime, _points[j].Easing);
            }

            while(--count > 0)
            {
                for(int j = 0; j < count; ++j)
                {
                    bp[j] = Quaternion.SlerpUnclamped(bp[j], bp[j+1], bt[j+1]);
                    bt[j] = (bt[j] + bt[j+1]) / 2;
                }
            }
            return bp[0];
        }

        // Kind of a sloppy way of implementing this, but hell if it works
        public float InterpolateLinear(float time)
        {
            if (_points == null || _points.Count == 0)
            {
                return 0;
            }

            if (_points.First().LinearPoint.y >= time)
            {
                return _points.First().LinearPoint.x;
            }

            if (_points.Last().LinearPoint.y <= time)
            {
                return _points.Last().LinearPoint.x;
            }

            SearchIndex(time, PropertyType.Linear, out int l, out int r);

            float normalTime = (time - _points[l].LinearPoint.y) / (_points[r].LinearPoint.y - _points[l].LinearPoint.y);
            float[] bp = new float[r-l+1];
            float[] bt = new float[r-l+1];
            int count = 0;
            for(int j = l; j <= r; ++j)
            {
                bp[count] = _points[j].LinearPoint.x;
                bt[count++] = Easings.Interpolate(normalTime, _points[j].Easing);
            }

            while(--count > 0)
            {
                for(int j = 0; j < count; ++j)
                {
                    bp[j] = Mathf.LerpUnclamped(bp[j], bp[j+1], bt[j+1]);
                    bt[j] = (bt[j] + bt[j+1]) / 2;
                }
            }
            return bp[0];
        }

        public Vector4 InterpolateVector4(float time)
        {
            if (_points == null || _points.Count == 0)
            {
                return Vector4.zero;
            }

            if (_points.First().Vector4Point.v >= time)
            {
                return _points.First().Vector4Point;
            }

            if (_points.Last().Vector4Point.v <= time)
            {
                return _points.Last().Vector4Point;
            }

            SearchIndex(time, PropertyType.Vector4, out int l, out int r);

            float normalTime = (time - _points[l].Vector4Point.v) / (_points[r].Vector4Point.v - _points[l].Vector4Point.v);
            Vector4[] bp = new Vector4[r-l+1];
            float[] bt = new float[r-l+1];
            int count = 0;
            for(int j = l; j <= r; ++j)
            {
                bp[count] = _points[j].Vector4Point;
                bt[count++] = Easings.Interpolate(normalTime, _points[j].Easing);
            }

            while(--count > 0)
            {
                for(int j = 0; j < count; ++j)
                {
                    bp[j] = Vector4.LerpUnclamped(bp[j], bp[j+1], bt[j+1]);
                    bt[j] = (bt[j] + bt[j+1]) / 2;
                }
            }
            return bp[0];
        }

        private static Vector3 SmoothVectorLerp(List<PointData> points, int a, int b, float time)
        {
            // Catmull-Rom Spline
            Vector3 p0 = a - 1 < 0 ? points[a].Point : points[a - 1].Point;
            Vector3 p1 = points[a].Point;
            Vector3 p2 = points[b].Point;
            Vector3 p3 = b + 1 > points.Count - 1 ? points[b].Point : points[b + 1].Point;

            float t = time;

            float tt = t * t;
            float ttt = tt * t;

            float q0 = -ttt + (2.0f * tt) - t;
            float q1 = (3.0f * ttt) - (5.0f * tt) + 2.0f;
            float q2 = (-3.0f * ttt) + (4.0f * tt) + t;
            float q3 = ttt - tt;

            Vector3 c = 0.5f * ((p0 * q0) + (p1 * q1) + (p2 * q2) + (p3 * q3));

            return c;
        }

        // Use binary search instead of linear search.
        private void SearchIndex(float time, PropertyType propertyType, out int l, out int r)
        {
            l = 0;
            r = _points.Count;

            while (l < r - 1)
            {
                int m = (l + r) / 2;
                float pointTime = 0;
                switch (propertyType)
                {
                    case PropertyType.Linear:
                        pointTime = _points[m].LinearPoint.y;
                        break;

                    case PropertyType.Quaternion:
                    case PropertyType.Vector3:
                        pointTime = _points[m].Point.w;
                        break;

                    case PropertyType.Vector4:
                        pointTime = _points[m].Vector4Point.v;
                        break;
                }

                if (pointTime < time)
                {
                    l = m;
                }
                else
                {
                    r = m;
                }
            }
            while(r < _points.Count - 1 && _points[r].Spline == Splines.splineBezier) // Skip over bézier control points
                ++r;
        }

        private void Add(PointData point)
        {
            if (_points == null)
            {
                _points = new List<PointData>();
            }

            _points.Add(point);
        }

        private struct Vector5
        {
            internal Vector5(float x, float y, float z, float w, float v)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
                this.v = v;
            }

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable SA1300 // Element should begin with upper-case letter
            internal float x { get; }

            internal float y { get; }

            internal float z { get; }

            internal float w { get; }

            internal float v { get; }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

            public static implicit operator Vector4(Vector5 vector)
            {
                return new Vector4(vector.x, vector.y, vector.z, vector.w);
            }
        }

        private class PointData
        {
            internal PointData(Vector4 point, Functions easing = Functions.easeLinear, Splines spline = Splines.splineNone)
            {
                Point = point;
                Easing = easing;
                Spline = spline;
            }

            internal PointData(Vector2 point, Functions easing = Functions.easeLinear, Splines spline = Splines.splineNone)
            {
                LinearPoint = point;
                Easing = easing;
                Spline = spline;
            }

            internal PointData(Vector5 point, Functions easing = Functions.easeLinear, Splines spline = Splines.splineNone)
            {
                Vector4Point = point;
                Easing = easing;
                Spline = spline;
            }

            internal Vector4 Point { get; }

            internal Vector5 Vector4Point { get; }

            internal Vector2 LinearPoint { get; }

            internal Functions Easing { get; }

            internal Splines Spline { get; }
        }
    }

    internal class PointDefinitionManager
    {
        internal IDictionary<string, PointDefinition> PointData { get; private set; } = new Dictionary<string, PointDefinition>();

        internal void AddPoint(string pointDataName, PointDefinition pointData)
        {
            if (!PointData.TryGetValue(pointDataName, out _))
            {
                PointData.Add(pointDataName, pointData);
            }
            else
            {
                NoodleLogger.Log($"Duplicate point defintion name, {pointDataName} could not be registered!", IPA.Logging.Logger.Level.Error);
            }
        }
    }
}
