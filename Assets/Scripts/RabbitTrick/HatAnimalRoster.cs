using System;
using UnityEngine;

namespace Sleight.RabbitTrick
{
    public enum HatSpecies { Mouse, Rabbit, Dove, Snake, Eagle, Monkey, Bear, Lion, Tiger, Elephant, Orca, EmperorPenguin, DivineDragon }

    public readonly struct HatAnimal
    {
        public readonly string Name, Sound;
        public readonly int Weight, Tier;
        public readonly Color Aura;
        public HatAnimal(string name, string sound, int weight, int tier, uint rgb)
        { Name = name; Sound = sound; Weight = weight; Tier = tier; Aura = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255); }
    }

    public readonly struct HatDraw
    {
        public readonly HatSpecies Species;
        public readonly bool Golden;
        public HatAnimal Animal => HatAnimalRoster.Animals[(int)Species];
        public float Percent => Animal.Weight / (Golden ? 10000f : 100f);
        public HatDraw(HatSpecies species, bool golden) { Species = species; Golden = golden; }
    }

    public static class HatAnimalRoster
    {
        public const int Tickets = 10000;
        public const float ClickInterval = 1f;
        public static readonly HatAnimal[] Animals = {
            new HatAnimal("MOUSE", "Mouse", 4000, 0, 0x111019),
            new HatAnimal("RABBIT", "Rabbit", 2000, 1, 0x9C9AA8),
            new HatAnimal("DOVE", "Dove", 1500, 2, 0xFFF6DF),
            new HatAnimal("SNAKE", "Snake", 1000, 3, 0x6BDE7F),
            new HatAnimal("EAGLE", "Eagle", 500, 4, 0x58E4E6),
            new HatAnimal("MONKEY", "Monkey", 300, 5, 0x618EFF),
            new HatAnimal("BEAR", "Bear", 200, 6, 0xB77AFF),
            new HatAnimal("LION", "Lion", 150, 7, 0xFF95C8),
            new HatAnimal("TIGER", "Tiger", 150, 7, 0xFF95C8),
            new HatAnimal("ELEPHANT", "Elephant", 90, 8, 0xFFAF56),
            new HatAnimal("ORCA", "Orca", 50, 9, 0xFF5166),
            new HatAnimal("EMPEROR PENGUIN", "EmperorPenguin", 50, 9, 0xFF5166),
            new HatAnimal("DIVINE DRAGON", "DivineDragon", 10, 10, 0xFFF0AD)
        };

        public static HatDraw Resolve(int animalTicket, int goldenTicket)
        {
            if (animalTicket < 0 || animalTicket >= Tickets) throw new ArgumentOutOfRangeException(nameof(animalTicket));
            if (goldenTicket < 0 || goldenTicket >= 100) throw new ArgumentOutOfRangeException(nameof(goldenTicket));
            int edge = 0;
            for (int i = 0; i < Animals.Length; i++)
            { edge += Animals[i].Weight; if (animalTicket < edge) return new HatDraw((HatSpecies)i, goldenTicket == 0); }
            throw new InvalidOperationException("Hat weights must total exactly 10000.");
        }
    }
}
