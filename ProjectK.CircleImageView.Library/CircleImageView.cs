/*
 * Copyright 2014 - 2020 Henning Dodenhof
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
// package de.hdodenhof.circleimageview;

//AK @SuppressWarnings("UnusedDeclaration")

using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Exception = Java.Lang.Exception;
using Math = Java.Lang.Math;
using Uri = Android.Net.Uri;

namespace ProjectK.Imaging
{
    public class CircleImageView : ImageView
    {
        #region Static

        private static readonly ScaleType DefaultScaleType = ScaleType.CenterCrop;
        private static readonly Bitmap.Config BitmapConfig = Bitmap.Config.Argb8888;
        private static readonly Color DefaultBorderColor = Color.Black;
        private static readonly Color DefaultCircleBackgroundColor = Color.Transparent;

        #endregion

        #region Const

        private const int ColorDrawableDimension = 2;
        private const int DefaultBorderWidth = 0;
        private const int DefaultImageAlpha = 255;
        private const bool DefaultBorderOverlay = false;

        #endregion

        #region Members

        private readonly Matrix _shaderMatrix = new Matrix();
        private readonly Paint _bitmapPaint = new Paint();
        private readonly Paint _borderPaint = new Paint();
        private readonly Paint _circleBackgroundPaint = new Paint();
        private readonly RectF _drawableRect = new RectF();
        private readonly RectF _borderRect = new RectF();
        private Color _borderColor = DefaultBorderColor;
        private Color _circleBackgroundColor = DefaultCircleBackgroundColor;
        private Bitmap _bitmap;
        private Canvas _bitmapCanvas;
        private ColorFilter _mColorFilter;
        private int _imageAlpha = DefaultImageAlpha;
        private int _borderWidth = DefaultBorderWidth;
        private float _drawableRadius;
        private float _borderRadius;
        private bool _disableCircularTransformation;
        private bool _initialized;
        private bool _rebuildShader;
        private bool _drawableDirty;
        private bool _borderOverlay;

        #endregion

        #region Properties

        public RectF BorderRect => _borderRect;

        public int BorderWidth
        {
            get => _borderWidth;
            set
            {
                if (value == _borderWidth)
                {
                    return;
                }

                _borderWidth = value;
                _borderPaint.StrokeWidth = value;
                UpdateDimensions();
                Invalidate();
            }
        }
        public bool IsBorderOverlay
        {
            get => _borderOverlay;
            set
            {
                if (value == _borderOverlay)
                {
                    return;
                }

                _borderOverlay = value;
                UpdateDimensions();
                Invalidate();
            }
        }
        public bool IsDisableCircularTransformation
        {
            get => _disableCircularTransformation;

            set
            {
                if (value == _disableCircularTransformation)
                {
                    return;
                }

                _disableCircularTransformation = value;

                if (value)
                {
                    _bitmap = null;
                    _bitmapCanvas = null;
                    _bitmapPaint.SetShader(null);
                }
                else
                {
                    InitializeBitmap();
                }

                Invalidate();
            }
        }


        #endregion

        #region Constructors

        public CircleImageView(Context context, IAttributeSet attrs) : this(context, attrs, 0)
        {
        }
        public CircleImageView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.CircleImageView, defStyle, 0);

            _borderWidth = a.GetDimensionPixelSize(Resource.Styleable.CircleImageView_civ_border_width, DefaultBorderWidth);
            _borderColor = a.GetColor(Resource.Styleable.CircleImageView_civ_border_color, DefaultBorderColor);
            _borderOverlay = a.GetBoolean(Resource.Styleable.CircleImageView_civ_border_overlay, DefaultBorderOverlay);
            _circleBackgroundColor = a.GetColor(Resource.Styleable.CircleImageView_civ_circle_background_color, DefaultCircleBackgroundColor);

            a.Recycle();

            Init();
        }
        protected CircleImageView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }


        #endregion

        #region Override

        public override void SetScaleType(ScaleType scaleType)
        {
            if (scaleType != DefaultScaleType)
            {
                throw new IllegalArgumentException($"ScaleType {scaleType} not supported.");
            }
        }
        public override void SetAdjustViewBounds(bool adjustViewBounds)
        {
            if (adjustViewBounds)
            {
                throw new IllegalArgumentException("adjustViewBounds not supported.");
            }
        }
        //AK @SuppressLint("CanvasSize")
        protected override void OnDraw(Canvas canvas)
        {
            if (_disableCircularTransformation)
            {
                base.OnDraw(canvas);
                return;
            }

            if (_circleBackgroundColor != Color.Transparent)
            {
                canvas.DrawCircle(_drawableRect.CenterX(), _drawableRect.CenterY(), _drawableRadius, _circleBackgroundPaint);
            }

            if (_bitmap != null)
            {
                if (_drawableDirty && _bitmapCanvas != null)
                {
                    _drawableDirty = false;
                    var drawable = Drawable;
                    if (drawable != null)
                    {
                        drawable.SetBounds(0, 0, _bitmapCanvas.Width, _bitmapCanvas.Height);
                        drawable.Draw(_bitmapCanvas);
                    }
                }

                if (_rebuildShader)
                {
                    _rebuildShader = false;
                    var tileX = Shader.TileMode.Clamp;
                    var tileY = Shader.TileMode.Clamp;
                    if (tileX != null && tileY != null)
                    {
                        var bitmapShader = new BitmapShader(_bitmap, tileX, tileY);
                        bitmapShader.SetLocalMatrix(_shaderMatrix);
                        _bitmapPaint.SetShader(bitmapShader);
                    }

                }

                canvas.DrawCircle(_drawableRect.CenterX(), _drawableRect.CenterY(), _drawableRadius, _bitmapPaint);
            }

            if (_borderWidth > 0)
            {
                canvas.DrawCircle(_borderRect.CenterX(), _borderRect.CenterY(), _borderRadius, _borderPaint);
            }
        }
        public override void InvalidateDrawable(/*AK @NonNull*/ Drawable dr)
        {
            _drawableDirty = true;
            Invalidate();
        }
        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            UpdateDimensions();
            Invalidate();
        }
        public override void SetPadding(int left, int top, int right, int bottom)
        {
            base.SetPadding(left, top, right, bottom);
            UpdateDimensions();
            Invalidate();
        }
        public override void SetPaddingRelative(int start, int top, int end, int bottom)
        {
            base.SetPaddingRelative(start, top, end, bottom);
            UpdateDimensions();
            Invalidate();
        }
        public override void SetImageBitmap(Bitmap bm)
        {
            base.SetImageBitmap(bm);
            InitializeBitmap();
            Invalidate();
        }
        public override void SetImageDrawable(Drawable drawable)
        {
            base.SetImageDrawable(drawable);
            InitializeBitmap();
            Invalidate();
        }
        public override void SetImageResource(/*AK @DrawableRes*/ int resId)
        {
            base.SetImageResource(resId);
            InitializeBitmap();
            Invalidate();
        }
        public override void SetImageURI(Uri uri)
        {
            base.SetImageURI(uri);
            InitializeBitmap();
            Invalidate();
        }
        public override int ImageAlpha
        {
            get => _imageAlpha;
            set
            {
                var alpha = value;
                alpha &= 0xFF;

                if (alpha == _imageAlpha)
                {
                    return;
                }

                _imageAlpha = alpha;

                // This might be called during ImageView construction before
                // member initialization has finished on API level >= 16.
                if (_initialized)
                {
                    _bitmapPaint.Alpha = alpha;
                    Invalidate();
                }
            }
        }
        public override ColorFilter ColorFilter => _mColorFilter;
        public override void SetColorFilter(ColorFilter cf)
        {
            if (cf == _mColorFilter)
            {
                return;
            }

            _mColorFilter = cf;

            // This might be called during ImageView construction before
            // member initialization has finished on API level <= 19.
            if (_initialized)
            {
                _bitmapPaint.SetColorFilter(cf);
                Invalidate();
            }
        }
        //Ak @SuppressLint("ClickableViewAccessibility")
        public override bool OnTouchEvent(MotionEvent e)
        {
            if (_disableCircularTransformation)
            {
                return base.OnTouchEvent(e);
            }

            return InTouchableArea(e.GetX(), e.GetY()) && base.OnTouchEvent(e);
        }

        #endregion

        #region Private functions
        private void Init()
        {
            _initialized = true;

            base.SetScaleType(DefaultScaleType);

            _bitmapPaint.AntiAlias = true;
            _bitmapPaint.Dither = true;
            _bitmapPaint.FilterBitmap = true;
            _bitmapPaint.Alpha = _imageAlpha;
            _bitmapPaint.SetColorFilter(_mColorFilter);

            _borderPaint.SetStyle(Paint.Style.Stroke);
            _borderPaint.AntiAlias = true;
            _borderPaint.Color = _borderColor;
            _borderPaint.StrokeWidth = _borderWidth;

            _circleBackgroundPaint.SetStyle(Paint.Style.Fill);
            _circleBackgroundPaint.AntiAlias = true;
            _circleBackgroundPaint.Color = _circleBackgroundColor;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                OutlineProvider = new OutlineProvider(this);
            }
        }

        private Bitmap GetBitmapFromDrawable(Drawable drawable)
        {
            if (drawable == null)
            {
                return null;
            }

            if (drawable is BitmapDrawable bitmapDrawable)
            {
                return bitmapDrawable.Bitmap;
            }

            try
            {
                var bitmap = drawable is ColorDrawable ?
                    Bitmap.CreateBitmap(ColorDrawableDimension, ColorDrawableDimension, BitmapConfig) :
                    Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, BitmapConfig);

                if (bitmap == null)
                    return null;

                var canvas = new Canvas(bitmap);
                drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
                drawable.Draw(canvas);
                return bitmap;
            }
            catch (Exception e)
            {
                e.PrintStackTrace();
                return null;
            }
        }
        private void InitializeBitmap()
        {
            _bitmap = GetBitmapFromDrawable(Drawable);

            if (_bitmap != null && _bitmap.IsMutable)
            {
                _bitmapCanvas = new Canvas(_bitmap);
            }
            else
            {
                _bitmapCanvas = null;
            }

            if (!_initialized)
            {
                return;
            }

            if (_bitmap != null)
            {
                UpdateShaderMatrix();
            }
            else
            {
                _bitmapPaint.SetShader(null);
            }
        }
        private void UpdateDimensions()
        {
            _borderRect.Set(CalculateBounds());
            _borderRadius = Math.Min((_borderRect.Height() - _borderWidth) / 2.0f, (_borderRect.Width() - _borderWidth) / 2.0f);

            _drawableRect.Set(_borderRect);
            if (!_borderOverlay && _borderWidth > 0)
            {
                _drawableRect.Inset(_borderWidth - 1.0f, _borderWidth - 1.0f);
            }

            _drawableRadius = Math.Min(_drawableRect.Height() / 2.0f, _drawableRect.Width() / 2.0f);

            UpdateShaderMatrix();
        }
        private RectF CalculateBounds()
        {
            var availableWidth = Width - PaddingLeft - PaddingRight;
            var availableHeight = Height - PaddingTop - PaddingBottom;

            var sideLength = Math.Min(availableWidth, availableHeight);

            var left = PaddingLeft + (availableWidth - sideLength) / 2f;
            var top = PaddingTop + (availableHeight - sideLength) / 2f;

            return new RectF(left, top, left + sideLength, top + sideLength);
        }
        private void UpdateShaderMatrix()
        {
            if (_bitmap == null)
            {
                return;
            }

            float scale;
            float dx = 0;
            float dy = 0;

            _shaderMatrix.Set(null);

            var bitmapHeight = _bitmap.Height;
            var bitmapWidth = _bitmap.Width;

            if (bitmapWidth * _drawableRect.Height() > _drawableRect.Width() * bitmapHeight)
            {
                scale = _drawableRect.Height() / bitmapHeight;
                dx = (_drawableRect.Width() - bitmapWidth * scale) * 0.5f;
            }
            else
            {
                scale = _drawableRect.Width() / bitmapWidth;
                dy = (_drawableRect.Height() - bitmapHeight * scale) * 0.5f;
            }

            _shaderMatrix.SetScale(scale, scale);
            _shaderMatrix.PostTranslate((int)(dx + 0.5f) + _drawableRect.Left, (int)(dy + 0.5f) + _drawableRect.Top);

            _rebuildShader = true;
        }

        private bool InTouchableArea(float x, float y)
        {
            if (_borderRect.IsEmpty)
            {
                return true;
            }

            return Math.Pow(x - _borderRect.CenterX(), 2) + Math.Pow(y - _borderRect.CenterY(), 2) <= Math.Pow(_borderRadius, 2);
        }

        // @RequiresApi(api = Build.VERSION_CODES.LOLLIPOP)

        #endregion

        #region Public functions

        public int GetBorderColor()
        {
            return _borderColor;
        }
        public void SetBorderColor(/* AK @ColorInt*/ Color borderColor)
        {
            if (borderColor == _borderColor)
            {
                return;
            }

            _borderColor = borderColor;
            _borderPaint.Color = borderColor;
            Invalidate();
        }
        public int GetCircleBackgroundColor()
        {
            return _circleBackgroundColor;
        }
        public void SetCircleBackgroundColor(Color circleBackgroundColor)
        {
            if (circleBackgroundColor == _circleBackgroundColor)
            {
                return;
            }

            _circleBackgroundColor = circleBackgroundColor;
            _circleBackgroundPaint.Color = circleBackgroundColor;
            Invalidate();
        }

        #endregion

    }
}
