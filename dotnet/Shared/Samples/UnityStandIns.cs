namespace UnityEngine
{
    /// <summary>
    /// A stand-in that carries the full name of Unity's colour type, so ClientRulesTests can plant colour constructions
    /// without Unity: the Client's colour rule judges a type by its full name. Compiled into the test assembly only.
    /// </summary>
    internal readonly struct Color
    {
        public Color(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public float Red { get; }

        public float Green { get; }

        public float Blue { get; }

        public float Alpha { get; }
    }

    /// <summary>A stand-in that carries the full name of Unity's 32-bit colour type, as <see cref="Color"/> does.</summary>
    internal readonly struct Color32
    {
        public Color32(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public byte Red { get; }

        public byte Green { get; }

        public byte Blue { get; }

        public byte Alpha { get; }
    }
}
