using System;
using System.Collections.Generic;

namespace SmartTaskbar.Models
{
    public static class BuiltinMatrices
    {
        public static float[,] Identity { get; }
        public static float[,] Negative { get; }
        public static float[,] GrayScale { get; }
        public static float[,] Sepia { get; }
        public static float[,] Red { get; }
        public static float[,] HueShift180 { get; }
        public static float[,] NegativeGrayScale { get; }
        public static float[,] NegativeSepia { get; }
        public static float[,] NegativeRed { get; }
        public static float[,] NegativeHueShift180 { get; }
        public static float[,] NegativeHueShift180Variation1 { get; }
        public static float[,] NegativeHueShift180Variation2 { get; }
        public static float[,] NegativeHueShift180Variation3 { get; }
        public static float[,] NegativeHueShift180Variation4 { get; }

        static BuiltinMatrices()
        {
            Identity = new float[,] {
                {  1.0f,  0.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  1.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  1.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  1.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  0.0f,  1.0f }
            };
            Negative = new float[,] {
                { -1.0f,  0.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f, -1.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f, -1.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  1.0f,  0.0f },
                {  1.0f,  1.0f,  1.0f,  0.0f,  1.0f }
            };
            GrayScale = new float[,] {
                {  0.3f,  0.3f,  0.3f,  0.0f,  0.0f },
                {  0.6f,  0.6f,  0.6f,  0.0f,  0.0f },
                {  0.1f,  0.1f,  0.1f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  1.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  0.0f,  1.0f }
            };
            
            float[,] redBase = new float[,] {
                {  1.0f,  0.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  0.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  1.0f,  0.0f },
                {  0.0f,  0.0f,  0.0f,  0.0f,  1.0f }
            };
            Red = Multiply(GrayScale, redBase);
            
            Sepia = new float[,] {
                { .393f, .349f, .272f, 0.0f, 0.0f},
                { .769f, .686f, .534f, 0.0f, 0.0f},
                { .189f, .168f, .131f, 0.0f, 0.0f},
                {  0.0f,  0.0f,  0.0f, 1.0f, 0.0f},
                {  0.0f,  0.0f,  0.0f, 0.0f, 1.0f}
            };
            HueShift180 = new float[,] {
                { -0.3333333f,  0.6666667f,  0.6666667f, 0.0f, 0.0f },
                {  0.6666667f, -0.3333333f,  0.6666667f, 0.0f, 0.0f },
                {  0.6666667f,  0.6666667f, -0.3333333f, 0.0f, 0.0f },
                {  0.0f,              0.0f,        0.0f, 1.0f, 0.0f },
                {  0.0f,              0.0f,        0.0f, 0.0f, 1.0f }
            };

            NegativeGrayScale = Multiply(Negative, GrayScale);
            NegativeSepia = Multiply(Negative, Sepia);
            NegativeRed = Multiply(NegativeGrayScale, redBase);
            NegativeHueShift180 = Multiply(Negative, HueShift180);

            NegativeHueShift180Variation1 = new float[,] {
                {  1.0f, -1.0f, -1.0f, 0.0f, 0.0f },
                { -1.0f,  1.0f, -1.0f, 0.0f, 0.0f },
                { -1.0f, -1.0f,  1.0f, 0.0f, 0.0f },
                {  0.0f,  0.0f,  0.0f, 1.0f, 0.0f },
                {  1.0f,  1.0f,  1.0f, 0.0f, 1.0f }
            };
            NegativeHueShift180Variation2 = new float[,] {
                {  0.39f, -0.62f, -0.62f, 0.0f, 0.0f },
                { -1.21f, -0.22f, -1.22f, 0.0f, 0.0f },
                { -0.16f, -0.16f,  0.84f, 0.0f, 0.0f },
                {   0.0f,   0.0f,   0.0f, 1.0f, 0.0f },
                {   1.0f,   1.0f,   1.0f, 0.0f, 1.0f }
            };
            NegativeHueShift180Variation3 = new float[,] {
                {     1.089508f,   -0.9326327f, -0.932633042f,  0.0f,  0.0f },
                {  -1.81771779f,    0.1683074f,  -1.84169245f,  0.0f,  0.0f },
                { -0.244589478f, -0.247815639f,    1.7621845f,  0.0f,  0.0f },
                {          0.0f,          0.0f,          0.0f,  1.0f,  0.0f },
                {          1.0f,          1.0f,          1.0f,  0.0f,  1.0f }
            };
            NegativeHueShift180Variation4 = new float[,] {
                {  0.50f, -0.78f, -0.78f, 0.0f, 0.0f },
                { -0.56f,  0.72f, -0.56f, 0.0f, 0.0f },
                { -0.94f, -0.94f,  0.34f, 0.0f, 0.0f },
                {   0.0f,   0.0f,   0.0f, 1.0f, 0.0f },
                {   1.0f,   1.0f,   1.0f, 0.0f, 1.0f }
            };
        }

        public static float[,] Multiply(float[,] a, float[,] b)
        {
            float[,] c = new float[5, 5];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        c[i, j] = c[i, j] + a[i, k] * b[k, j];
                    }
                }
            }
            return c;
        }

        public static float[,] GetMatrixByName(string name)
        {
            return name switch
            {
                "Negative" => Negative,
                "GrayScale" => GrayScale,
                "Sepia" => Sepia,
                "Red" => Red,
                "NegativeGrayScale" => NegativeGrayScale,
                "NegativeSepia" => NegativeSepia,
                "NegativeRed" => NegativeRed,
                "NegativeHueShift180" => NegativeHueShift180,
                "NegativeHueShift180Variation1" => NegativeHueShift180Variation1,
                "NegativeHueShift180Variation2" => NegativeHueShift180Variation2,
                "NegativeHueShift180Variation3" => NegativeHueShift180Variation3,
                "NegativeHueShift180Variation4" => NegativeHueShift180Variation4,
                _ => Identity
            };
        }
    }
}
