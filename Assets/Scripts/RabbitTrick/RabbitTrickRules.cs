using UnityEngine;

namespace Sleight.RabbitTrick
{
    public enum TrickStep { ShowEmpty, HideRabbit, RevealRabbit, Complete }
    public enum HandItem { None, Rabbit, Lid, Box, Dancer, Custom }   // Custom：第 2、3 关的道具，具体是哪个看 TrickHand.custom

    // Gameplay state is independent of input, rendering and Unity's frame order.
    public sealed class RabbitTrickRules
    {
        public TrickStep Step { get; private set; }
        public bool RabbitInside { get; private set; }
        public bool RabbitTouched { get; private set; }
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; }
        public bool Finished => Step == TrickStep.Complete;
        public float EmptyShowProgress { get; private set; }

        public void Reset()
        {
            Step = TrickStep.ShowEmpty;
            RabbitInside = RabbitTouched = Failed = false;
            FailureReason = "";
            EmptyShowProgress = 0f;
        }

        public void TouchRabbit() => RabbitTouched = true;

        public void ShowEmpty(float delta, bool fullyOpen, bool boxLit)
        {
            if (Failed || Step != TrickStep.ShowEmpty) return;
            EmptyShowProgress = fullyOpen && boxLit && !RabbitInside
                ? Mathf.Min(1f, EmptyShowProgress + delta / 0.8f) : 0f;
            if (EmptyShowProgress >= 1f) Step = TrickStep.HideRabbit;
        }

        public bool TryInsert(bool fullyOpen, bool rabbitLit, bool openingLit)
        {
            if (Failed || Step != TrickStep.HideRabbit || !fullyOpen || RabbitInside) return false;
            if (rabbitLit || openingLit)
            {
                Expose("The audience saw you slip the rabbit into the box.");
                return false;
            }
            RabbitInside = true;
            RabbitTouched = true;
            return true;
        }

        public void CloseBox(bool fullyClosed)
        {
            if (!Failed && Step == TrickStep.HideRabbit && RabbitInside && fullyClosed)
                Step = TrickStep.RevealRabbit;
        }

        public bool TryReveal(bool fullyOpen, bool boxLit)
        {
            if (Failed || Step != TrickStep.RevealRabbit || !RabbitInside || !fullyOpen || !boxLit)
                return false;
            Step = TrickStep.Complete;
            return true;
        }

        public void CheckExposure(bool rabbitLit, bool lidOpen)
        {
            if (Failed || Finished || !RabbitTouched) return;
            // Once it has been secretly loaded AND closed, opening it is the advertised trick.
            if (RabbitInside && Step == TrickStep.RevealRabbit) return;
            if (rabbitLit && (!RabbitInside || lidOpen))
                Expose(RabbitInside ? "The lid was open: they saw the rabbit hidden inside." : "The spotlight caught the rabbit in your hand.");
        }

        public void Expose(string reason)
        {
            if (Finished || Failed) return;
            Failed = true;
            FailureReason = reason;
        }
    }
}
