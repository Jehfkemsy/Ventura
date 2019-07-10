﻿using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Ventura.Exceptions;
using Ventura.Interfaces;

using static Ventura.Constants;

namespace Ventura
{
	internal class RNGVenturaServiceProvider : RandomNumberGenerator, IRNGVenturaServiceProvider
	{
		private readonly IAccumulator accumulator;
		private readonly IGenerator generator;
		private readonly Stream stream;
		private readonly object syncRoot = new object();
		private DateTimeOffset lastReseedTime = DateTimeOffset.MinValue;
		private int reseedCounter;
		private Timer reseedTimer;
		private bool isDisposed;

		public RNGVenturaServiceProvider(IAccumulator accumulator, IGenerator generator, Stream stream)
		{
			this.accumulator = accumulator ?? throw new ArgumentNullException();
			this.generator = generator ?? throw new ArgumentNullException();
			this.stream = stream ?? throw new ArgumentException();

			if (!stream.CanRead || !stream.CanSeek || !stream.CanWrite)
					throw new ArgumentException("Stream must be readable/writable/seekable");
		}

		/// <summary>
		/// Waits for accumulator to generate enough entropy then
		/// reads the first 64 bytes from the seed stream and uses it to reseed the generator.
		/// Runs a task on regular intervals to update the seed. 
		/// </summary>
		public void Initialise()
		{
			var seed = new byte[SeedFileSize];
			stream.ReadAsync(seed, 0, SeedFileSize);

			while (!accumulator.HasEnoughEntropy)
			{
				Thread.Sleep(1);
			}

			generator.Reseed(seed);
			reseedTimer = new Timer(UpdateSeed, null, 0, SeedUpdateInterval.Milliseconds);
		}

		/// <summary>
		/// Returns data from generator, reseeds every time pool 0 has enough entropy or
		/// a set amount of time (100ms according to spec) has passed between reseeds
		/// </summary>
		public override void GetBytes(byte[] data)
		{
			lock (syncRoot)
			{
				var timeSinceLastReseed = DateTime.UtcNow - lastReseedTime;

				if (accumulator.HasEnoughEntropy && timeSinceLastReseed > MaximumTimeSpanBetweenReseeds)
				{
					reseedCounter++;
					Reseed(accumulator.GetRandomDataFromPools(reseedCounter));
					Debug.WriteLine($"Reseeding completed! Counter: {reseedCounter}");
				}

				if (reseedCounter == 0)
				{
					throw new GeneratorSeedException("Generator not seeded yet!");
				}

				generator.GenerateData(data);
			}
		}

		/// <summary>
		/// Uses the generator to get 64 bytes of random data
		/// and turns them to a positive integer
		/// </summary>
		/// <param name="min">minimum possible value, equal to or greater than zero </param>
		/// <param name="max">maximum possible value</param>
		/// <returns>pseudo-randomly generated positive integer</returns>
		public int GetRandomNumber(int min, int max)
		{
			if (min < 0)
				throw new ArgumentException("Less than zero not supported");

			byte[] data = new byte[4];
			GetBytes(data);

			int num = Math.Abs(BitConverter.ToInt32(data, 0));

			return (num % (max - min)) + min;
		}

		/// <summary>
		/// Returns an array of the specified length with
		/// pseudo-randomly generated positive integers
		/// </summary>
		/// <param name="min">minimum possible value, equal to or greater than zero</param>
		/// <param name="max">maximum possible value</param>
		public int[] GetRandomNumbers(int min, int max, int length)
		{
			if (min < 0)
				throw new ArgumentException("Less than zero not supported");

			if (length <= 0) 
				throw new ArgumentException(nameof(length));

			var result = new int[length];

			for (int i = 0; i < length; i++)
			{
				result[i] = GetRandomNumber(min, max);
			}

			return result;
		}

		/// <summary>
		/// Returns a 64-bit floating point value ranging from 0 to 1
		/// </summary>
		/// <param name="roundToDecimals">decimal place to round to</param>
		public double GetRandomNumber(int roundToDecimals = 0)
		{
			if (roundToDecimals < 0)
				throw new ArgumentException(nameof(roundToDecimals));

			byte[] data = new byte[8];
			GetBytes(data);

			return Math.Round(((double) BitConverter.ToUInt64(data, 0) / ulong.MaxValue), roundToDecimals);
		}

		/// <summary>
		/// Returns an array of 64-bit floating point values ranging from 0 to 1
		/// </summary>
		/// <param name="roundToDecimals">decimal place to round to</param>
		/// <param name="length">array length</param>
		public double[] GetRandomNumbers(int roundToDecimals, int length)
		{
			if (roundToDecimals < 0)
				throw new ArgumentException(nameof(roundToDecimals));

			if (length <= 0)
				throw new ArgumentException(nameof(length));

			var result = new double[length];

			for (int i = 0; i < length; i++)
			{
				result[i] = GetRandomNumber(roundToDecimals);
			}

			return result;
		}

		#region Private implementation

		/// <summary>
		/// Updates the seed one final time,
		/// closes the stream, un-registers events and stops the timer
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			if (isDisposed) return;

			UpdateSeed(null);
			stream.Close();
			accumulator.Dispose();
			reseedTimer.Dispose();

			base.Dispose(disposing);
			isDisposed = true;
		}

		private void Reseed(byte[] seed)
		{
			generator.Reseed(seed);
			lastReseedTime = DateTimeOffset.UtcNow;
		}

		private void UpdateSeed(object state)
		{
			var data = new byte[SeedFileSize];
			GetBytes(data);
			stream.Seek(0, SeekOrigin.Begin);
			stream.Write(data, 0, SeedFileSize);
			stream.Flush();
		}

		#endregion
	}
}