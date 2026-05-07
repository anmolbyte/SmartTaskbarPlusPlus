using System;
using SmartTaskbar.Models;

namespace SmartTaskbar.Helpers
{
    public static class MagnificationManager
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (!_isInitialized)
            {
                _isInitialized = Fun.MagInitialize();
            }
        }

        public static void Uninitialize()
        {
            if (_isInitialized)
            {
                Fun.MagUninitialize();
                _isInitialized = false;
            }
        }

        public static void SetColorEffect(float[,] matrix)
        {
            Initialize();
            var effect = new Fun.ColorEffect(matrix);
            Fun.MagSetFullscreenColorEffect(ref effect);
        }

        public static void RestoreDefault()
        {
            if (_isInitialized)
            {
                SetColorEffect(BuiltinMatrices.Identity);
            }
        }
    }
}
