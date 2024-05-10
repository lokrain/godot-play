using System;
using System.Collections.Generic;
using Godot;

namespace Libarary.PerlinNoise2D
{

    public partial class PerlinNoise2D
    {
        private List<int> _permutationTable = new();

        private RandomNumberGenerator _rng = new();
        private ulong _seed;

        [Export]
        public ulong Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                _rng.Seed = _seed;

                _permutationTable.Clear();

                for (int i = 0; i < 256; i++)
                {
                    _permutationTable.Add(i);
                }

                for (int i = 0; i < 256; i++)
                {
                    int j = _rng.RandiRange(0, 255);
                    int temp = _permutationTable[i];
                    _permutationTable[i] = _permutationTable[j];
                    _permutationTable[j] = temp;
                }

                _permutationTable.AddRange(_permutationTable);
            }
        }

        [Export]
        public Func<float, float, Godot.Vector2> Gradient => (float x, float y) =>
        {
            var vec2 = new Vector2(x, y) * Frequency % Region.Size;
            var perm = _permutationTable[((int)vec2.X << 8) | (int)vec2.Y];
            var angle = perm * 2 * Mathf.Pi / 256;
            return new Vector2(x + Mathf.Cos(angle), y + Mathf.Sin(angle)).Normalized();
        };

        [Export]
        public Rect2 Region { get; private set; } = new Rect2(0, 0, 1, 1);

        [Export]
        public int Frequency { get; private set; } = 1;

        [Export]
        public int Octaves { get; private set; } = 1;

        [Export]
        public float Persistence { get; private set; } = 0.5f;

        [Export]
        public float Lacunarity { get; private set; } = 2.0f;

        public PerlinNoise2D(ulong seed, Rect2 region, int frequency, int octaves, float persistence, float lacunarity)
        {
            Seed = seed;
            Region = region;
            Frequency = frequency;
            Octaves = octaves;
            Persistence = persistence;
            Lacunarity = lacunarity;
        }

        public float GetNoiseValue(float x, float y, int octaves)
        {
            float total = 0f;
            float frequency = Frequency;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            // Loop through each octave
            for (int i = 0; i < octaves; i++)
            {
                // Compute the noise for the current octave
                total += CalculateNoise(x * frequency, y * frequency) * amplitude;

                // Accumulate the maximum amplitude (for normalization)
                maxAmplitude += amplitude;

                // Increase frequency and decrease amplitude for the next octave
                frequency *= Lacunarity;
                amplitude *= Persistence;
            }

            // Normalize the final noise value
            return total / maxAmplitude;
        }

        private float CalculateNoise(float x, float y)
        {
            // Get the integer coordinates of the grid cell
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;

            // Compute the relative coordinates within the grid cell
            float xf = x - (float)Math.Floor(x);
            float yf = y - (float)Math.Floor(y);

            // Compute the fade curves for the interpolation
            float u = Fade(xf);
            float v = Fade(yf);

            // Compute the hashes of the corners of the grid cell
            int aa = _permutationTable[xi] + yi;
            int ab = _permutationTable[xi] + yi + 1;
            int ba = _permutationTable[xi + 1] + yi;
            int bb = _permutationTable[xi + 1] + yi + 1;

            // Calculate the gradient vectors at the corners of the grid cell
            Vector2 gradAA = Gradient(xi, yi);
            Vector2 gradAB = Gradient(xi, yi + 1);
            Vector2 gradBA = Gradient(xi + 1, yi);
            Vector2 gradBB = Gradient(xi + 1, yi + 1);

            // Compute the dot products of the gradient vectors and the relative coordinates
            float dotAA = gradAA.Dot(new Vector2(xf, yf));
            float dotAB = gradAB.Dot(new Vector2(xf, yf - 1));
            float dotBA = gradBA.Dot(new Vector2(xf - 1, yf));
            float dotBB = gradBB.Dot(new Vector2(xf - 1, yf - 1));

            // Interpolate the values in the x and y directions
            float lerpX1 = Lerp(dotAA, dotBA, u);
            float lerpX2 = Lerp(dotAB, dotBB, u);
            float finalNoise = Lerp(lerpX1, lerpX2, v);

            // Return the final noise value
            return finalNoise;
        }

        private float Fade(float t)
        {
            // Fade function to smooth the interpolation
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private float Lerp(float a, float b, float t)
        {
            // Linear interpolation function
            return a + t * (b - a);
        }

    }
}