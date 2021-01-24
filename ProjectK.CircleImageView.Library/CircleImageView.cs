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

//import android.annotation.SuppressLint;
//import android.content.Context;
//import android.content.res.TypedArray;
//import android.graphics.Bitmap;
//import android.graphics.BitmapShader;
//import android.graphics.Canvas;
//import android.graphics.Color;
//import android.graphics.ColorFilter;
//import android.graphics.Matrix;
//import android.graphics.Outline;
//import android.graphics.Paint;
//import android.graphics.Rect;
//import android.graphics.RectF;
//import android.graphics.Shader;
//import android.graphics.drawable.BitmapDrawable;
//import android.graphics.drawable.ColorDrawable;
//import android.graphics.drawable.Drawable;
//import android.net.Uri;
//import android.os.Build;
//import android.util.IAttributeSet;
//import android.view.MotionEvent;
//import android.view.View;
//import android.view.ViewOutlineProvider;
//import android.widget.ImageView;
//import androidx.annotation.ColorInt;
//import androidx.annotation.ColorRes;
//import androidx.annotation.DrawableRes;
//import androidx.annotation.NonNull;
//import androidx.annotation.RequiresApi;



//AK @SuppressWarnings("UnusedDeclaration")


using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Net;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace ProjectK.CircleImageView.Library
{
    public class CircleImageView : ImageView
    {

        private static ScaleType SCALE_TYPE = ScaleType.CenterCrop;
        private static Bitmap.Config BITMAP_CONFIG = Bitmap.Config.Argb8888;
        private const int COLORDRAWABLE_DIMENSION = 2;

        private const int DEFAULT_BORDER_WIDTH = 0;
        private static Color DEFAULT_BORDER_COLOR = Color.Black;
        private static Color DEFAULT_CIRCLE_BACKGROUND_COLOR = Color.Transparent;
        private const int DEFAULT_IMAGE_ALPHA = 255;
        private const bool DEFAULT_BORDER_OVERLAY = false;

        private RectF mDrawableRect = new RectF();
        public RectF mBorderRect = new RectF();

        private Matrix mShaderMatrix = new Matrix();
        private Paint mBitmapPaint = new Paint();
        private Paint mBorderPaint = new Paint();
        private Paint mCircleBackgroundPaint = new Paint();

        private Color mBorderColor =   DEFAULT_BORDER_COLOR;
        private int mBorderWidth = DEFAULT_BORDER_WIDTH;
        private Color mCircleBackgroundColor = DEFAULT_CIRCLE_BACKGROUND_COLOR;
        private int mImageAlpha = DEFAULT_IMAGE_ALPHA;

        private Bitmap mBitmap;
        private Canvas mBitmapCanvas;

        private float mDrawableRadius;
        private float mBorderRadius;

        private ColorFilter mColorFilter;

        private bool mInitialized;
        private bool mRebuildShader;
        private bool mDrawableDirty;

        private bool mBorderOverlay;
        public bool mDisableCircularTransformation;

        public CircleImageView(Context context) : base(context)
        {
            init();
        }

        public CircleImageView(Context context, IAttributeSet attrs) : this(context, attrs, 0)
        {
        }

        public CircleImageView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.CircleImageView, defStyle, 0);

            mBorderWidth = a.GetDimensionPixelSize(Resource.Styleable.CircleImageView_civ_border_width, DEFAULT_BORDER_WIDTH);
            mBorderColor = a.GetColor(Resource.Styleable.CircleImageView_civ_border_color, DEFAULT_BORDER_COLOR);
            mBorderOverlay = a.GetBoolean(Resource.Styleable.CircleImageView_civ_border_overlay, DEFAULT_BORDER_OVERLAY);
            mCircleBackgroundColor = a.GetColor(Resource.Styleable.CircleImageView_civ_circle_background_color, DEFAULT_CIRCLE_BACKGROUND_COLOR);

            a.Recycle();

            init();
        }

        private void init()
        {
            mInitialized = true;

            base.SetScaleType(SCALE_TYPE);

            mBitmapPaint.AntiAlias = true;
            mBitmapPaint.Dither = true;
            mBitmapPaint.FilterBitmap = true;
            mBitmapPaint.Alpha = mImageAlpha;
            mBitmapPaint.SetColorFilter(mColorFilter);

            mBorderPaint.SetStyle(Android.Graphics.Paint.Style.Stroke);
            mBorderPaint.AntiAlias = true;
            mBorderPaint.Color = mBorderColor;
            mBorderPaint.StrokeWidth = mBorderWidth;

            mCircleBackgroundPaint.SetStyle(Android.Graphics.Paint.Style.Fill);
            mCircleBackgroundPaint.AntiAlias = true;
            mCircleBackgroundPaint.Color = mCircleBackgroundColor;

            if (Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
            {
                OutlineProvider = new OutlineProvider(this);
            }
        }

        public override void SetScaleType(ScaleType scaleType)
        {
            if (scaleType != SCALE_TYPE)
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
                    var drawable = this.Drawable;
                    drawable.SetBounds(0, 0, mBitmapCanvas.Width, mBitmapCanvas.Height);
                    drawable.Draw(mBitmapCanvas);
                }

                if (mRebuildShader)
                {
                    mRebuildShader = false;

                    var bitmapShader = new BitmapShader(mBitmap, Shader.TileMode.Clamp, Shader.TileMode.Clamp);
                    bitmapShader.SetLocalMatrix(mShaderMatrix);

                    mBitmapPaint.SetShader(bitmapShader);
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

        public int getBorderColor()
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

        /**
     * @deprecated Use {@link #setCircleBackgroundColor(int)} instead
     */
        //AK @Deprecated
        public void SetCircleBackgroundColorResource(/*AK @ColorRes*/ int circleBackgroundRes)
        {
            SetCircleBackgroundColor(Context.Resources.GetColor(circleBackgroundRes));
        }

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

        private Bitmap getBitmapFromDrawable(Drawable drawable)
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
                Bitmap bitmap;

                if (drawable is ColorDrawable colorDrawable)
                {
                    bitmap = Bitmap.CreateBitmap(COLORDRAWABLE_DIMENSION, COLORDRAWABLE_DIMENSION, BITMAP_CONFIG);
                }
                else
                {
                    bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, BITMAP_CONFIG);
                }

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
            mBitmap = getBitmapFromDrawable(Drawable);

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
                scale = mDrawableRect.Height() / (float) bitmapHeight;
                dx = (mDrawableRect.Width() - bitmapWidth * scale) * 0.5f;
            }
            else
            {
                scale = mDrawableRect.Width() / (float) bitmapWidth;
                dy = (mDrawableRect.Height() - bitmapHeight * scale) * 0.5f;
            }

            mShaderMatrix.SetScale(scale, scale);
            mShaderMatrix.PostTranslate((int) (dx + 0.5f) + mDrawableRect.Left, (int) (dy + 0.5f) + mDrawableRect.Top);

            mRebuildShader = true;
        }

        //Ak @SuppressLint("ClickableViewAccessibility")

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (mDisableCircularTransformation)
            {
                return base.OnTouchEvent(e);
            }

            return inTouchableArea(e.GetX(), e.GetY()) && base.OnTouchEvent(e);
        }

        private bool inTouchableArea(float x, float y)
        {
            if (mBorderRect.IsEmpty)
            {
                return true;
            }

            return Math.Pow(x - mBorderRect.CenterX(), 2) + Math.Pow(y - mBorderRect.CenterY(), 2) <= Math.Pow(mBorderRadius, 2);
        }

        // @RequiresApi(api = Build.VERSION_CODES.LOLLIPOP)
    }
}
