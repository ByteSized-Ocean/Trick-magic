using UnityEngine;

namespace Sleight.RabbitTrick
{
    public enum HatExitStyle { Scurry, Hop, Slither, Flight, Run, Leap, Stampede, IceSlide, Swim, Spiral }

    public struct HatAnimalPose
    {
        // Positions use the menu's 1600 x 900 reference space, with Y pointing down.
        public Vector2 Position, Scale;
        public float Rotation, Alpha, DepartureAge, Progress;
        public bool Departing, Finished;
    }

    public static class HatAnimalMotion
    {
        public static HatExitStyle Style(HatSpecies species)
        {
            switch (species)
            {
                case HatSpecies.Mouse: return HatExitStyle.Scurry;
                case HatSpecies.Rabbit: return HatExitStyle.Hop;
                case HatSpecies.Snake: return HatExitStyle.Slither;
                case HatSpecies.Dove: case HatSpecies.Eagle: return HatExitStyle.Flight;
                case HatSpecies.Monkey: return HatExitStyle.Leap;
                case HatSpecies.Elephant: return HatExitStyle.Stampede;
                case HatSpecies.EmperorPenguin: return HatExitStyle.IceSlide;
                case HatSpecies.Orca: return HatExitStyle.Swim;
                case HatSpecies.DivineDragon: return HatExitStyle.Spiral;
                default: return HatExitStyle.Run;
            }
        }
        public static float ExitAt(HatSpecies species) => species == HatSpecies.DivineDragon ? 3.2f : 2.4f;
        public static float ExitDuration(HatSpecies species)
        {
            if (species == HatSpecies.Mouse || species == HatSpecies.Rabbit || species == HatSpecies.Dove) return 4.5f;
            switch (Style(species))
            {
                case HatExitStyle.Spiral: return 10.5f;
                case HatExitStyle.Swim: return 8.5f;
                case HatExitStyle.IceSlide: return 8f;
                case HatExitStyle.Leap: return 7.8f;
                default: return 7.2f;
            }
        }
        public static Vector2 Hold(HatSpecies species) => species == HatSpecies.DivineDragon ? new Vector2(800, 460) : new Vector2(645, 621);

        public static HatAnimalPose Sample(HatSpecies species, float age, int route, float exitAt = -1)
        {
            if (exitAt < 0) exitAt = ExitAt(species);
            float duration = ExitDuration(species), time = Mathf.Max(0, age - exitAt), p = Mathf.Clamp01(time / duration);
            var style = Style(species); var hold = Hold(species);
            var pose = new HatAnimalPose { Scale = Vector2.one, Alpha = Mathf.Clamp01(age / 0.18f), Position = hold,
                DepartureAge = time, Progress = p, Departing = age >= exitAt, Finished = age > exitAt + duration };
            if (!pose.Departing)
            {
                float appear = Mathf.Clamp01(age / 0.75f), ease = 1 - Mathf.Pow(1 - appear, 3);
                pose.Position = Vector2.Lerp(new Vector2(397, 627), hold, ease) - Vector2.up * (Mathf.Sin(appear * Mathf.PI) * 75 + Mathf.Sin(age * 3.5f) * 5);
                pose.Scale = Vector2.one * Mathf.Lerp(0.05f, 1, ease); pose.Rotation = Mathf.Sin(age * 3) * 3;
                return pose;
            }
            int side = (route & 1) == 0 ? -1 : 1;
            float q = Mathf.Pow(p, 1.18f);
            Vector2 destination, tangent;
            switch (style)
            {
                case HatExitStyle.Scurry: case HatExitStyle.Hop: case HatExitStyle.Slither:
                    destination = (route % 4) == 0 ? new Vector2(-240, 700) : (route % 4) == 1 ? new Vector2(1840, 720)
                        : (route % 4) == 2 ? new Vector2(520, -230) : new Vector2(800, 1140);
                    tangent = destination - hold; pose.Position = Vector2.Lerp(hold, destination, q);
                    pose.Rotation = Heading(tangent, out float facing); pose.Scale.x = facing;
                    float bounce = style == HatExitStyle.Hop ? Mathf.Abs(Mathf.Sin(time * 11)) * 15 : style == HatExitStyle.Scurry ? Mathf.Sin(time * 22) * 3 : Mathf.Sin(time * 7) * 5;
                    pose.Position.y -= bounce * Mathf.Clamp01(time * 3); break;
                case HatExitStyle.Flight:
                    destination = new Vector2(side > 0 ? 1920 : -320, -250 - (route % 3) * 50);
                    pose.Position = Vector2.Lerp(hold, destination, q); pose.Position.y -= Mathf.Sin(p * Mathf.PI) * 190;
                    pose.Scale.x = side; pose.Rotation = side * (10 + Mathf.Sin(time * 3) * 8); break;
                case HatExitStyle.Run: case HatExitStyle.Stampede:
                    destination = new Vector2(side > 0 ? 1900 : -300, 630 + (route % 3) * 24);
                    pose.Position = Vector2.Lerp(hold, destination, q); pose.Position.y -= Mathf.Abs(Mathf.Sin(time * (style == HatExitStyle.Stampede ? 8 : 13))) * (style == HatExitStyle.Stampede ? 6 : 10);
                    pose.Scale.x = side; pose.Scale.y = 1 - Mathf.Sin(time * 14) * 0.025f; break;
                case HatExitStyle.Leap:
                    destination = new Vector2(side > 0 ? 1870 : -270, 665);
                    pose.Position = Vector2.Lerp(hold, destination, q); pose.Position.y -= Mathf.Abs(Mathf.Sin(p * Mathf.PI * 4)) * 210;
                    pose.Scale.x = side; pose.Rotation = side * Mathf.Sin(p * Mathf.PI * 8) * 14; break;
                case HatExitStyle.IceSlide:
                    destination = new Vector2(side > 0 ? 1860 : -260, 680);
                    pose.Position = Vector2.Lerp(hold, destination, q); pose.Position.y += Mathf.Sin(p * Mathf.PI * 3) * 12;
                    pose.Rotation = -side * Mathf.SmoothStep(0, 90, Mathf.Clamp01(time / 0.9f));
                    pose.Scale.y = 1 - 0.06f * Mathf.Sin(time * 8); break;
                case HatExitStyle.Swim:
                    destination = new Vector2(side > 0 ? 1890 : -290, 540 + (route % 3) * 55);
                    pose.Position = Vector2.Lerp(hold, destination, q); pose.Position.y += Mathf.Sin(p * Mathf.PI * 4) * 75;
                    pose.Scale.x = side; pose.Rotation = -side * Mathf.Cos(p * Mathf.PI * 4) * 12; break;
                default:
                    float angle = p * Mathf.PI * 4 * side, radius = 50 + p * 330;
                    var circle = hold + new Vector2(Mathf.Sin(angle) * radius, (1 - Mathf.Cos(angle)) * radius * 0.48f);
                    destination = new Vector2(side > 0 ? 2000 : -500, -450);
                    pose.Position = Vector2.Lerp(circle, destination, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.76f, 1, p)));
                    pose.Scale = Vector2.one * (1 - p * 0.25f); pose.Scale.x *= side;
                    pose.Rotation = side * (Mathf.Sin(angle) * 24 + p * 8); break;
            }
            // Fade only once the animal has travelled beyond the screen, never halfway along its exit.
            pose.Alpha *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.965f, 1, p));
            return pose;
        }
        static float Heading(Vector2 direction, out float facing)
        {
            facing = direction.x >= 0 ? 1 : -1;
            float angle = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg;
            return facing < 0 ? Mathf.DeltaAngle(180, angle) : angle;
        }
    }
}
