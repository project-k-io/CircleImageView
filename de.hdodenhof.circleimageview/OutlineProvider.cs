using Android.Graphics;
using Android.Views;

namespace de.hdodenhof.circleimageview
{
    public class OutlineProvider : ViewOutlineProvider
    {
        private readonly CircleImageView mView;
        public OutlineProvider(CircleImageView view)
        {
            mView = view;
        }

        public override void GetOutline(View view, Outline outline)
        {
            if (mView.IsDisableCircularTransformation)
            {
                Background?.GetOutline(view, outline);
            }
            else
            {
                var bounds = new Rect();
                mView.BorderRect.RoundOut(bounds);
                outline.SetRoundRect(bounds, bounds.Width() / 2.0f);
            }
        }
    }
}