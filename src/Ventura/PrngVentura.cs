﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Ventura.Interfaces;

using static Ventura.Constants;

namespace Ventura
{
    internal class PrngVentura : IPrngVentura, IDisposable
    {
        private readonly IAccumulator accumulator;
        private readonly IGenerator generator;
        private DateTimeOffset lastReseedTime = DateTimeOffset.MinValue;
        private int reseedCounter = 1;

        public PrngVentura(IAccumulator accumulator, IGenerator generator)
        {
            this.accumulator = accumulator;
            this.generator = generator;
        }
        
        public byte[] GetRandomData(byte[] input)
        {
			var timeSinceLastReseed = DateTime.UtcNow - lastReseedTime;

			if (accumulator.HasEnoughEntropy && timeSinceLastReseed > MaximumTimeSpanBetweenReseeds)
			{
				// Reseed the Generator
				Reseed(accumulator.GetRandomDataFromPools(reseedCounter));
				Debug.WriteLine($"Reseeding completed! Counter: { reseedCounter }");
			}

			return generator.GenerateData(input);
        }

        private void Reseed(byte[] seed)
        {
	        generator.Reseed(seed);
	        lastReseedTime = DateTimeOffset.UtcNow;
	        reseedCounter++;
        }

		public int GetRandomNumber()
        {
            throw new NotImplementedException();
        }

        public int[] GetRandomNumbers()
        {
            throw new NotImplementedException();
        }

        public string GetRandomString(int length)
        {
            throw new NotImplementedException();
        }

        public string[] GetRandomStrings(int length)
        {
            throw new NotImplementedException();
        }

        public string[] GetRandomStrings(int minStringLength, int maxStringLength, int arrayLength)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
			accumulator.Dispose();
        }
    }
}