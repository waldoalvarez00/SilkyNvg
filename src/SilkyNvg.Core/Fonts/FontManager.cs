﻿using FontStash.NET;
using Silk.NET.Maths;
using SilkyNvg.Common;
using SilkyNvg.Core.States;
using SilkyNvg.Rendering;
using System;
using System.Collections.Generic;

namespace SilkyNvg.Core.Fonts
{
    internal class FontManager : IDisposable
    {

        private const uint INIT_FONTIMAGE_SIZE = 512;
        private const uint MAX_FONTIMAGE_SIZE = 2048;
        private const uint MAX_FONTIMAGES = 4;

        private readonly int[] _fontImages = new int[(int)MAX_FONTIMAGES];
        private readonly Nvg _nvg;

        private int _fontImageIdx;

        public Fontstash Fontstash { get; }

        public FontManager(Nvg nvg)
        {
            _nvg = nvg;

            for (uint i = 0; i < MAX_FONTIMAGES; i++)
            {
                _fontImages[i] = 0;
            }

            FonsParams fontParams = new()
            {
                width = (int)INIT_FONTIMAGE_SIZE,
                height = (int)INIT_FONTIMAGE_SIZE,
                flags = (byte)FonsFlags.ZeroTopleft,
                renderCreate = null,
                renderUpdate = null,
                renderDraw = null,
                renderDelete = null
            };
            Fontstash = new Fontstash(fontParams);

            _fontImages[0] = _nvg.renderer.CreateTexture(Texture.Alpha, new Vector2D<uint>((uint)fontParams.width, (uint)fontParams.height), 0, null);
            if (_fontImages[0] == 0)
            {
                _nvg.Dispose();
                throw new Exception("Failed to create dummy font atlas!");
            }
            _fontImageIdx = 0;
        }

        public float GetFontScale()
        {
            return MathF.Min(Maths.Quantize(Maths.GetAverageScale(_nvg.stateStack.CurrentState.Transform), 0.01f), 4.0f);
        }

        public void FlushTextTexture()
        {
            if (Fontstash.ValidateTexture(out int[] dirty))
            {
                int fontImage = _fontImages[_fontImageIdx];
                if (fontImage != 0)
                {
                    byte[] data = Fontstash.GetTextureData(out _, out _);
                    int x = dirty[0];
                    int y = dirty[1];
                    int w = dirty[2] - dirty[0];
                    int h = dirty[3] - dirty[1];
                    _nvg.renderer.UpdateTexture(fontImage, new Rectangle<uint>(new Vector2D<uint>((uint)x, (uint)y), new Vector2D<uint>((uint)w, (uint)h)), data);
                }
            }
        }

        public void Pack()
        {
            if (_fontImageIdx != 0)
            {
                int fontImage = _fontImages[_fontImageIdx];

                if (fontImage == 0)
                {
                    return;
                }

                _nvg.renderer.GetTextureSize(fontImage, out Vector2D<uint> iSize);
                uint i, j;
                for (i = j = 0; i < _fontImageIdx; i++)
                {
                    if (_fontImages[i] != 0)
                    {
                        _nvg.renderer.GetTextureSize(_fontImages[i], out Vector2D<uint> nSize);
                        if (nSize.X < iSize.X || nSize.Y < iSize.Y)
                        {
                            _nvg.renderer.DeleteTexture(_fontImages[i]);
                        }
                        else
                        {
                            _fontImages[j++] = _fontImages[i];
                        }
                    }
                }
                _fontImages[j++] = _fontImages[0];
                _fontImages[0] = fontImage;
                _fontImageIdx = 0;
                for (i = j; i < MAX_FONTIMAGES; i++)
                {
                    _fontImages[i] = 0;
                }
            }
        }

        public bool AllocTextAtlas()
        {
            FlushTextTexture();
            if (_fontImageIdx >= MAX_FONTIMAGES - 1)
            {
                return false;
            }

            Vector2D<uint> iSize;

            if (_fontImages[_fontImageIdx + 1] != 0)
            {
                _nvg.renderer.GetTextureSize(_fontImages[_fontImageIdx + 1], out iSize);
            }
            else
            {
                _nvg.renderer.GetTextureSize(_fontImages[_fontImageIdx], out iSize);
                if (iSize.X > iSize.Y)
                {
                    iSize.Y *= 2;
                }
                else
                {
                    iSize.X *= 2;
                }

                if (iSize.X > MAX_FONTIMAGE_SIZE || iSize.Y > MAX_FONTIMAGE_SIZE)
                {
                    iSize = new(MAX_FONTIMAGE_SIZE);
                }

                _fontImages[_fontImageIdx + 1] = _nvg.renderer.CreateTexture(Texture.Alpha, iSize, 0, null);
            }
            _fontImageIdx++;
            Fontstash.ResetAtlas((int)iSize.X, (int)iSize.Y);
            return true;
        }

        public void RenderText(ICollection<Vertex> vertices)
        {
            State state = _nvg.stateStack.CurrentState;
            Paint paint = state.Fill;

            paint = Paint.ForText(_fontImages[_fontImageIdx], paint);

            paint.PremultiplyAlpha(state.Alpha);

            _nvg.renderer.Triangles(paint, state.CompositeOperation, state.Scissor, vertices, _nvg.pixelRatio.FringeWidth);

            _nvg.FrameMeta.Update((uint)vertices.Count / 3, 0, 0, 1);
        }

        public void Dispose()
        {
            Fontstash.Dispose();

            for (uint i = 0; i < MAX_FONTIMAGES; i++)
            {
                if (_fontImages[i] != 0)
                {
                    _nvg.renderer.DeleteTexture(_fontImages[i]);
                    _fontImages[i] = 0;
                }
            }
        }

    }
}
