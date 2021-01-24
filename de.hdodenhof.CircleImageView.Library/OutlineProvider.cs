using Android.Graphics;
using Android.Views;

namespace de.hdodenhof.circleimageview
{
    public class OutlineProvider : ViewOutlineProvider
    {
        private readonly CircleImageView _view;
        public OutlineProvider(CircleImageView view)
        {
            _view = view;
        }

        public override void GetOutline(View view, Outline outline)
        {
            if (_view.mDisableCircularTransformation)
            {
                Background.GetOutline(view, outline);
            }
            else
            {
                var bounds = new Rect();
                _view.mBorderRect.RoundOut(bounds);
                outline.SetRoundRect(bounds, bounds.Width() / 2.0f);
            }
        }
    }
}