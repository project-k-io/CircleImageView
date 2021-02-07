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

namespace de.hdodenhof.circleimageview
{
    public class CircleImageView : ImageView
    {
        #region Static

        private static readonly ScaleType DefaultScaleType = ScaleType.CenterCrop;
        private static readonly Bitmap.Config BitmapConfig = Bitmap.Config.Argb8888;
        private static readonly Color DefaultBorderColor = Color.Black;
        private static readonly Color DefaultCircleBackgroundColor = Color.Transparent;

        #endregion

        #region Constants

        private const int ColorDrawableDimension = 2;
        private const int DefaultBorderWidth = 0;
        private const int DefaultImageAlpha = 255;
        private const bool DefaultBorderOverlay = false;

        #endregion

        #region Private Members

        private readonly RectF mDrawableRect = new RectF();
        private readonly Matrix mShaderMatrix = new Matrix();
        private readonly Paint mBitmapPaint = new Paint();
        private readonly Paint mBorderPaint = new Paint();
        private readonly Paint mCircleBackgroundPaint = new Paint();
        private readonly RectF mBorderRect = new RectF();
        private Bitmap mBitmap;
        private Canvas mBitmapCanvas;
        private Color mBorderColor = DefaultBorderColor;
        private Color mCircleBackgroundColor = DefaultCircleBackgroundColor;
        private ColorFilter mColorFilter;
        private float mDrawableRadius;
        private float mBorderRadius;
        private int mImageAlpha = DefaultImageAlpha;
        private int mBorderWidth = DefaultBorderWidth;
        private bool mInitialized;
        private bool mRebuildShader;
        private bool mDrawableDirty;
        private bool mBorderOverlay;
        private bool mDisableCircularTransformation;

        #endregion

        #region Properties

        public RectF BorderRect => mBorderRect;

        public int BorderWidth
        {
            get => mBorderWidth;
            set
            {
                if (value == mBorderWidth)
                {
                    return;
                }

                mBorderWidth = value;
                mBorderPaint.StrokeWidth = value;
                UpdateDimensions();
                Invalidate();
            }
        }

        public bool IsBorderOverlay
        {
            get => mBorderOverlay;
            set
            {
                if (value == mBorderOverlay)
                {
                    return;
                }

                mBorderOverlay = value;
                UpdateDimensions();
                Invalidate();
            }
        }

        public bool IsDisableCircularTransformation
        {
            get => mDisableCircularTransformation;

            set
            {
                if (value == mDisableCircularTransformation)
                {
                    return;
                }

                mDisableCircularTransformation = value;

                if (value)
                {
                    mBitmap = null;
                    mBitmapCanvas = null;
                    mBitmapPaint.SetShader(null);
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

        public CircleImageView(Context context) : base(context)
        {
            Init();
        }

        public CircleImageView(Context context, IAttributeSet attrs) : this(context, attrs, 0)
        {
        }

        public CircleImageView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.CircleImageView, defStyle, 0);

            mBorderWidth = a.GetDimensionPixelSize(Resource.Styleable.CircleImageView_civ_border_width, DefaultBorderWidth);
            mBorderColor = a.GetColor(Resource.Styleable.CircleImageView_civ_border_color, DefaultBorderColor);
            mBorderOverlay = a.GetBoolean(Resource.Styleable.CircleImageView_civ_border_overlay, DefaultBorderOverlay);
            mCircleBackgroundColor = a.GetColor(Resource.Styleable.CircleImageView_civ_circle_background_color, DefaultCircleBackgroundColor);

            a.Recycle();

            Init();
        }

        public CircleImageView(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }

        protected CircleImageView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }


        #endregion

        #region Override Functions

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
            if (mDisableCircularTransformation)
            {
                base.OnDraw(canvas);
                return;
            }

            if (mCircleBackgroundColor != Color.Transparent)
            {
                canvas.DrawCircle(mDrawableRect.CenterX(), mDrawableRect.CenterY(), mDrawableRadius, mCircleBackgroundPaint);
            }

            if (mBitmap != null)
            {
                if (mDrawableDirty && mBitmapCanvas != null)
                {
                    mDrawableDirty = false;
                    var drawable = Drawable;
                    if (drawable != null)
                    {
                        drawable.SetBounds(0, 0, mBitmapCanvas.Width, mBitmapCanvas.Height);
                        drawable.Draw(mBitmapCanvas);
                    }
                }

                if (mRebuildShader)
                {
                    mRebuildShader = false;
                    var tileX = Shader.TileMode.Clamp;
                    var tileY = Shader.TileMode.Clamp;
                    if (tileX != null && tileY != null)
                    {
                        var bitmapShader = new BitmapShader(mBitmap, tileX, tileY);
                        bitmapShader.SetLocalMatrix(mShaderMatrix);
                        mBitmapPaint.SetShader(bitmapShader);
                    }
                }

                canvas.DrawCircle(mDrawableRect.CenterX(), mDrawableRect.CenterY(), mDrawableRadius, mBitmapPaint);
            }

            if (mBorderWidth > 0)
            {
                canvas.DrawCircle(mBorderRect.CenterX(), mBorderRect.CenterY(), mBorderRadius, mBorderPaint);
            }
        }
        public override void InvalidateDrawable(/*AK @NonNull*/ Drawable dr)
        {
            mDrawableDirty = true;
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
            get => mImageAlpha;
            set
            {
                var alpha = value;
                alpha &= 0xFF;

                if (alpha == mImageAlpha)
                {
                    return;
                }

                mImageAlpha = alpha;

                // This might be called during ImageView construction before
                // member initialization has finished on API level >= 16.
                if (mInitialized)
                {
                    mBitmapPaint.Alpha = alpha;
                    Invalidate();
                }
            }
        }
        public override ColorFilter ColorFilter => mColorFilter;
        public override void SetColorFilter(ColorFilter cf)
        {
            if (cf == mColorFilter)
            {
                return;
            }

            mColorFilter = cf;

            // This might be called during ImageView construction before
            // member initialization has finished on API level <= 19.
            if (mInitialized)
            {
                mBitmapPaint.SetColorFilter(cf);
                Invalidate();
            }
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (mDisableCircularTransformation)
            {
                return base.OnTouchEvent(e);
            }

            return InTouchableArea(e.GetX(), e.GetY()) && base.OnTouchEvent(e);
        }

        #endregion

        #region Private Functions

        private void Init()
        {
            mInitialized = true;

            base.SetScaleType(DefaultScaleType);

            mBitmapPaint.AntiAlias = true;
            mBitmapPaint.Dither = true;
            mBitmapPaint.FilterBitmap = true;
            mBitmapPaint.Alpha = mImageAlpha;
            mBitmapPaint.SetColorFilter(mColorFilter);

            mBorderPaint.SetStyle(Paint.Style.Stroke);
            mBorderPaint.AntiAlias = true;
            mBorderPaint.Color = mBorderColor;
            mBorderPaint.StrokeWidth = mBorderWidth;

            mCircleBackgroundPaint.SetStyle(Paint.Style.Fill);
            mCircleBackgroundPaint.AntiAlias = true;
            mCircleBackgroundPaint.Color = mCircleBackgroundColor;

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
            mBitmap = GetBitmapFromDrawable(Drawable);

            if (mBitmap != null && mBitmap.IsMutable)
            {
                mBitmapCanvas = new Canvas(mBitmap);
            }
            else
            {
                mBitmapCanvas = null;
            }

            if (!mInitialized)
            {
                return;
            }

            if (mBitmap != null)
            {
                UpdateShaderMatrix();
            }
            else
            {
                mBitmapPaint.SetShader(null);
            }
        }
        private void UpdateDimensions()
        {
            mBorderRect.Set(CalculateBounds());
            mBorderRadius = Math.Min((mBorderRect.Height() - mBorderWidth) / 2.0f, (mBorderRect.Width() - mBorderWidth) / 2.0f);

            mDrawableRect.Set(mBorderRect);
            if (!mBorderOverlay && mBorderWidth > 0)
            {
                mDrawableRect.Inset(mBorderWidth - 1.0f, mBorderWidth - 1.0f);
            }

            mDrawableRadius = Math.Min(mDrawableRect.Height() / 2.0f, mDrawableRect.Width() / 2.0f);

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
            if (mBitmap == null)
            {
                return;
            }

            float scale;
            float dx = 0;
            float dy = 0;

            mShaderMatrix.Set(null);

            var bitmapHeight = mBitmap.Height;
            var bitmapWidth = mBitmap.Width;

            if (bitmapWidth * mDrawableRect.Height() > mDrawableRect.Width() * bitmapHeight)
            {
                scale = mDrawableRect.Height() / bitmapHeight;
                dx = (mDrawableRect.Width() - bitmapWidth * scale) * 0.5f;
            }
            else
            {
                scale = mDrawableRect.Width() / bitmapWidth;
                dy = (mDrawableRect.Height() - bitmapHeight * scale) * 0.5f;
            }

            mShaderMatrix.SetScale(scale, scale);
            mShaderMatrix.PostTranslate((int)(dx + 0.5f) + mDrawableRect.Left, (int)(dy + 0.5f) + mDrawableRect.Top);

            mRebuildShader = true;
        }
        private bool InTouchableArea(float x, float y)
        {
            if (mBorderRect.IsEmpty)
            {
                return true;
            }

            return Math.Pow(x - mBorderRect.CenterX(), 2) + Math.Pow(y - mBorderRect.CenterY(), 2) <= Math.Pow(mBorderRadius, 2);
        }
        #endregion

        #region Public Functions

        public int GetBorderColor()
        {
            return mBorderColor;
        }

        public void SetBorderColor(/* AK @ColorInt*/ Color borderColor)
        {
            if (borderColor == mBorderColor)
            {
                return;
            }

            mBorderColor = borderColor;
            mBorderPaint.Color = borderColor;
            Invalidate();
        }

        public int GetCircleBackgroundColor()
        {
            return mCircleBackgroundColor;
        }

        public void SetCircleBackgroundColor(Color circleBackgroundColor)
        {
            if (circleBackgroundColor == mCircleBackgroundColor)
            {
                return;
            }

            mCircleBackgroundColor = circleBackgroundColor;
            mCircleBackgroundPaint.Color = circleBackgroundColor;
            Invalidate();
        }

        #endregion
    }
}
