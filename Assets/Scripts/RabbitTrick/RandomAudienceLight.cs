using UnityEngine;
namespace Sleight.RabbitTrick
{
    public sealed class RandomAudienceLight : MonoBehaviour
    {
        public float bottomRadius = 1.45f, topRadius = 0.24f, floorY = 0.85f, topY = 5.8f;
        // 难度：追光每次停多久、移动用多久（RabbitTrickGame 按关卡设置）
        public float dwellMin = 2.8f, dwellMax = 4.6f, travelMin = 1.1f, travelMax = 1.7f;
        // 开场演示时由脚本指挥追光（ScriptMove），不随机走
        public bool Scripted { get; set; }
        // 偏向舞台左边：每次挑下一个落点时，有 leftChance 的机会去左边 [leftMin, leftMax]，并且在那里停得更久
        public float leftChance, leftMin = 1.4f, leftMax = 3.0f, leftDwellMin = 6f, leftDwellMax = 9f;
        public void SetLeftBias(float chance, float dwellMin, float dwellMax) { leftChance = chance; leftDwellMin = dwellMin; leftDwellMax = dwellMax; }
        public void ScriptMove(float x, float duration) => MoveTo(x, duration);
        public void SetPace(float minDwell, float maxDwell, float minTravel, float maxTravel)
        { dwellMin = minDwell; dwellMax = maxDwell; travelMin = minTravel; travelMax = maxTravel; }
        public float X { get; private set; }
        public float DanceRemaining { get; private set; }
        public bool Moving => travelElapsed < travelDuration;
        public bool Dancing => DanceRemaining > 0f;
        public bool DancerDragging { get; set; }
        public float DancerX { get; private set; } = 8.25f;
        float fromX, targetX, travelElapsed, travelDuration, dwellRemaining, danceTime, anchorX;
        public void ResetLight(float boxX)
        {
            X = fromX = targetX = boxX; travelElapsed = travelDuration = 0;
            dwellRemaining = 9f; DanceRemaining = danceTime = 0;
            DancerX = anchorX = 8.25f; DancerDragging = false; ApplyShader();
        }
        public void Distract(float dancerX, float duration = 12f)
        { DancerX = anchorX = dancerX; DanceRemaining = duration; danceTime = 0; MoveTo(dancerX, 0.85f); }
        public void SetDancerX(float x) { DancerX = anchorX = Mathf.Clamp(x, 1.1f, 8.65f); DancerDragging = true; }
        public void Tick(float dt)
        {
            if (Scripted)
            {
                if (Moving)
                {
                    travelElapsed = Mathf.Min(travelDuration, travelElapsed + dt);
                    X = Mathf.Lerp(fromX, targetX, Mathf.SmoothStep(0, 1, travelElapsed / travelDuration));
                }
                ApplyShader();
                return;
            }
            bool wasDancing = Dancing; DanceRemaining = Mathf.Max(0, DanceRemaining - dt);
            if (Dancing)
            {
                danceTime += dt;
                if (!DancerDragging) DancerX = Mathf.Clamp(anchorX + Mathf.Sin(danceTime * 1.7f) * 0.35f, 1.1f, 8.65f);
                if (Moving)
                {
                    travelElapsed = Mathf.Min(travelDuration, travelElapsed + dt);
                    X = Mathf.Lerp(fromX, DancerX, Mathf.SmoothStep(0, 1, travelElapsed / travelDuration));
                }
                else X = Mathf.MoveTowards(X, DancerX, dt * 6f);
            }
            else if (Moving)
            {
                travelElapsed = Mathf.Min(travelDuration, travelElapsed + dt);
                X = Mathf.Lerp(fromX, targetX, Mathf.SmoothStep(0, 1, travelElapsed / travelDuration));
            }
            else
            {
                if (wasDancing) dwellRemaining = 1f;
                dwellRemaining -= dt;
                if (dwellRemaining <= 0)
                {
                    if (leftChance > 0f && X > leftMax && Random.value < leftChance)
                    {
                        MoveTo(Random.Range(leftMin, leftMax), Random.Range(travelMin, travelMax));
                        dwellRemaining = Random.Range(leftDwellMin, leftDwellMax);
                    }
                    else
                    {
                        float next = Random.Range(3.1f, 8.4f);
                        if (Mathf.Abs(next - X) < 1.2f) next = X > 5.5f ? 3.4f : 8;
                        MoveTo(next, Random.Range(travelMin, travelMax)); dwellRemaining = Random.Range(dwellMin, dwellMax);
                    }
                }
            }
            ApplyShader();
        }
        void MoveTo(float x, float duration) { fromX = X; targetX = x; travelElapsed = 0; travelDuration = Mathf.Max(0.01f, duration); }
        public float RadiusAt(float y) => Mathf.Lerp(bottomRadius, topRadius, Mathf.InverseLerp(floorY, topY, y));
        public bool Illuminates(Vector2 point, float radius = 0) => point.y >= floorY - radius && point.y <= topY + radius && Mathf.Abs(point.x - X) <= RadiusAt(point.y) + radius;
        // Cape bounds are conservative: the whole rabbit must fit, not just its centre.
        public bool CoversRabbit(Vector2 point) => Dancing && Mathf.Abs(point.x - DancerX) <= 0.32f && point.y >= 1.55f && point.y <= 2.18f;
        public bool BlocksBox(Vector2 point) => Dancing && Mathf.Abs(point.x - DancerX) < 0.85f;
        public bool RabbitVisibleInLight(Vector2 point) => Illuminates(point, 0.24f) && !CoversRabbit(point);
        void ApplyShader()
        {
            Shader.SetGlobalVector("_RabbitSpot", new Vector4(X, floorY, topY, bottomRadius));
            Shader.SetGlobalFloat("_RabbitSpotTopRadius", topRadius);
        }
    }
}
