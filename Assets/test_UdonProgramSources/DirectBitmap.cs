
using System.Runtime.InteropServices;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class DirectBitmap : UdonSharpBehaviour
{
       public Texture2D Bitmap;
        public Color[] Bits;
        public bool Disposed;
        public readonly int Height = 144;
        public readonly int Width = 160;
        public Color[] clear;

        public void Start() {
            Bits = new Color[Width * Height];
            clear = new Color[256 * 256];
            for (int i = 0; i != clear.Length; ++ i)
            {
                clear[i] = new Color(1f, 1f, 1f, 0f);
            }
        }

        public void SetPixel(int x, int y, Color colour) 
        {
            int index = x + (y * Width);
            Bits[index] = colour;
        }

        public Color GetPixel(int x, int y) {
            int index = x + (y * Width);
            return Bits[index];
        }

        public void Render()
        {
            //clear color first
            Bitmap.SetPixels(clear);
            for(int x = 0; x != Width; ++x)
            {
                for(int y = 0; y != Height; ++y)
                {
                    int index = x + (y * Width);
                    Bitmap.SetPixel(x, Height - y, Bits[index]);
                }
            }
            Bitmap.Apply();
        }

    }

