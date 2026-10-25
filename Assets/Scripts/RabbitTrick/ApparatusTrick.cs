using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sleight.RabbitTrick
{
    // Physical controls shared by the new acts. Task progress never enables or disables a control.
    public abstract class ApparatusTrick : StageTrick
    {
        protected sealed class Control
        {
            public int id;
            public Vector2 origin, travel, axis;
            public float depth, drag;
            public Func<float> read;
            public Action<float> write;
            public Transform visual;
            public Vector2 Point => origin + travel * read();
        }
        readonly List<Control> controls = new List<Control>();
        readonly Dictionary<int, LooseProp> props = new Dictionary<int, LooseProp>();
        LooseProp[] propCache = Array.Empty<LooseProp>();
        protected override LooseProp[] MovableProps => propCache;
        protected static readonly Color Gold = new Color32(210, 156, 79, 255), Cream = new Color32(242, 222, 188, 255), Teal = new Color32(100, 220, 199, 255);
        protected static readonly Color Wine = new Color32(126, 48, 77, 255), Ink = new Color32(25, 21, 36, 255);
        protected const float Floor = DualHandController.FrontY;

        protected void Lever(int id, Vector2 origin, Vector2 travel, Vector2 axis, Func<float> read, Action<float> write, float drag = 0.7f, float depth = 0)
        {
            var control = new Control { id = id, origin = origin, travel = travel, axis = axis.normalized, read = read, write = write, drag = drag, depth = depth };
            var guide = view.Line("ControlRail" + id, root, 0.045f, new Color(0.36f, 0.29f, 0.37f), 21, 2);
            guide.SetPosition(0, origin); guide.SetPosition(1, origin + travel);
            control.visual = view.Disc("Control" + id, root, control.Point, new Vector2(0.17f, 0.17f), Gold, 24).transform;
            controls.Add(control);
        }
        protected void Prop(int id, LooseProp prop)
        { props.Add(id, prop); propCache = new List<LooseProp>(props.Values).ToArray(); }
        protected static void Place(LooseProp prop, Vector2 position, float depth = 0)
        {
            prop.position = position; prop.depth = depth; prop.heldBy = -1; prop.inside = false;
            prop.velocity = Vector2.zero; prop.floorY = prop.frontFloor = Floor;
            prop.floorY += depth * DualHandController.LaneLift;
        }
        public Vector2 ControlPoint(int id) => controls.Find(c => c.id == id).Point;
        public float ControlValue(int id) => controls.Find(c => c.id == id).read();
        protected int HeldBy(int id)
        {
            for (int i = 0; i < 2; i++)
                if (game.Hands.Hands[i].holding == HandItem.Custom && game.Hands.Hands[i].custom == id) return i;
            return -1;
        }
        protected virtual bool Accessible(LooseProp prop) => true;
        protected virtual void Taken(LooseProp prop) { }
        protected virtual void Dropped(LooseProp prop) { }
        public override bool TryGrab(TrickHand hand, int index)
        {
            float best = float.MaxValue; int id = -1;
            foreach (var entry in props)
            {
                var p = entry.Value;
                if (!Accessible(p) || !game.Hands.AtDepth(p.depth) || p.Held && p.heldBy != 1 - index) continue;
                float distance = p.DistanceTo(hand.position);
                if (distance <= 0.23f && distance < best) { best = distance; id = entry.Key; }
            }
            foreach (var c in controls)
            {
                if (!game.Hands.AtDepth(c.depth)) continue;
                float distance = Vector2.Distance(hand.position, c.Point);
                if (distance <= 0.27f && distance < best && Vector2.Distance(game.Hands.Shoulder(index), c.Point) <= game.Hands.Reach)
                { best = distance; id = c.id; }
            }
            if (id < 0) return false;
            hand.holding = HandItem.Custom; hand.custom = id;
            if (props.TryGetValue(id, out var prop))
            {
                if (prop.Held) game.Hands.Hands[prop.heldBy].holding = HandItem.None;
                prop.Grab(index, hand.position); Taken(prop);
            }
            else hand.position = ControlPoint(id);
            return true;
        }
        public override void MoveHeld(TrickHand hand, int index, Vector2 delta)
        {
            if (props.TryGetValue(hand.custom, out var prop))
            { hand.position = game.Hands.ClampHand(index, hand.position + delta); prop.Follow(hand.position); return; }
            var control = controls.Find(c => c.id == hand.custom);
            if (control == null) return;
            float value = Mathf.Clamp01(control.read() + Vector2.Dot(delta, control.axis) / control.drag);
            if (Vector2.Distance(game.Hands.Shoulder(index), control.origin + control.travel * value) <= game.Hands.Reach) control.write(value);
            for (int i = 0; i < 2; i++)
                if (game.Hands.Hands[i].holding == HandItem.Custom && game.Hands.Hands[i].custom == control.id) game.Hands.Hands[i].position = control.Point;
        }
        public override void Release(TrickHand hand, int index)
        { if (props.TryGetValue(hand.custom, out var prop)) { prop.Drop(); Dropped(prop); } }
        public override bool Anchor(TrickHand hand, out Vector2 position)
        {
            var control = controls.Find(c => c.id == hand.custom);
            position = control == null ? Vector2.zero : control.Point;
            return control != null;
        }
        public override float AnchorDepth(TrickHand hand) => controls.Find(c => c.id == hand.custom)?.depth ?? 0;
        protected void AdvanceProps(float dt)
        {
            SyncHeldProps();
            foreach (var prop in propCache) { FollowLane(prop, Floor); prop.Tick(dt); }
        }
        protected void DrawControls()
        { foreach (var c in controls) { c.visual.position = c.Point; StageDepth.Set(c.visual, c.depth, 2); } }
        protected Transform Frame(string name, Vector2 center, Vector2 size, Color color)
        {
            var frame = view.Group(name, root, center);
            view.Rect("Interior", frame, Vector2.zero, size, Ink, 12);
            for (int side = -1; side <= 1; side += 2)
            {
                view.Rect("Post", frame, Vector2.right * side * size.x * 0.5f, new Vector2(0.07f, size.y + 0.08f), color, 15);
                view.Rect("Trim", frame, Vector2.up * side * size.y * 0.5f, new Vector2(size.x + 0.12f, 0.07f), color, 15);
            }
            return frame;
        }
        protected Transform Person(string name, Color dress)
        {
            var p = view.Group(name, root, Vector2.zero);
            view.Rect("Dress", p, new Vector2(0, 0.7f), new Vector2(0.43f, 0.65f), dress, 20);
            view.Rect("Sash", p, new Vector2(0, 0.84f), new Vector2(0.45f, 0.05f), Gold, 21);
            for (int side = -1; side <= 1; side += 2)
            {
                view.Rect("Leg", p, new Vector2(side * 0.08f, 0.21f), new Vector2(0.06f, 0.4f), Cream, 19);
                view.Rect("Arm", p, new Vector2(side * 0.26f, 0.8f), new Vector2(0.065f, 0.42f), Cream, 19);
            }
            view.Disc("Head", p, new Vector2(0, 1.18f), new Vector2(0.29f, 0.31f), Cream, 22);
            view.Disc("Hair", p, new Vector2(0, 1.29f), new Vector2(0.3f, 0.15f), Wine, 23);
            return p;
        }
        protected void Bar(Transform image, Vector2 left, float width, float amount)
        {
            float fill = Mathf.Clamp01(amount);
            image.position = left + Vector2.right * width * fill * 0.5f;
            var scale = image.localScale; scale.x = Mathf.Max(0.002f, width * fill) / view.square.bounds.size.x; image.localScale = scale;
        }
        protected void Cover(Transform image, Vector2 bottom, Vector2 size, float closed)
        {
            image.gameObject.SetActive(closed > 0.01f);
            image.position = bottom + Vector2.up * size.y * closed * 0.5f;
            var scale = image.localScale; scale.y = size.y * Mathf.Max(0.002f, closed) / view.square.bounds.size.y; image.localScale = scale;
        }
    }
}
