﻿using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SkiaSharp.Tests
{
	public class SKCodecTest : SKTest
	{
		[Fact]
		public void MinBufferedBytesNeededHasAValue ()
		{
			Assert.True (SKCodec.MinBufferedBytesNeeded > 0);
		}

		[Fact]
		public void CanCreateStreamCodec ()
		{
			var stream = new SKFileStream (Path.Combine (PathToImages, "color-wheel.png"));
			using (var codec = SKCodec.Create (stream)) {
				Assert.Equal (SKEncodedFormat.Png, codec.EncodedFormat);
				Assert.Equal (128, codec.Info.Width);
				Assert.Equal (128, codec.Info.Height);
				Assert.Equal (SKAlphaType.Unpremul, codec.Info.AlphaType);
				if (IsUnix) {
					Assert.Equal (SKColorType.Rgba8888, codec.Info.ColorType);
				} else {
					Assert.Equal (SKColorType.Bgra8888, codec.Info.ColorType);
				}
			}
		}
		
		[Fact]
		public void GetGifFrames ()
		{
			const int FrameCount = 16;

			var stream = new SKFileStream (Path.Combine (PathToImages, "animated-heart.gif"));
			using (var codec = SKCodec.Create (stream)) {
				Assert.Equal (-1, codec.RepetitionCount);

				var frameInfos = codec.FrameInfo;
				Assert.Equal (FrameCount, frameInfos.Length);

				Assert.Equal (-1, frameInfos [0].RequiredFrame);

				var cachedFrames = new SKBitmap [FrameCount];
				var info = new SKImageInfo (codec.Info.Width, codec.Info.Height);

				var decode = new Action<SKBitmap, bool, int> ((bm, cached, index) => {
					if (cached) {
						var requiredFrame = frameInfos [index].RequiredFrame;
						if (requiredFrame != -1) {
							Assert.True (cachedFrames [requiredFrame].CopyTo (bm));
						}
					}
					var opts = new SKCodecOptions (index, cached);
					var result = codec.GetPixels (info, bm.GetPixels (), opts);
					Assert.Equal (SKCodecResult.Success, result);
				});

				for (var i = 0; i < FrameCount; i++) {
					var cachedFrame = cachedFrames [i] = new SKBitmap (info);
					decode (cachedFrame, true, i);

					var uncachedFrame = new SKBitmap (info);
					decode (uncachedFrame, false, i);

					Assert.Equal (cachedFrame.Bytes, uncachedFrame.Bytes);
				}
			}
		}

		[Fact]
		public void GetEncodedInfo ()
		{
			var stream = new SKFileStream (Path.Combine (PathToImages, "color-wheel.png"));
			using (var codec = SKCodec.Create (stream)) {
				Assert.Equal (SKEncodedInfoColor.Rgba, codec.EncodedInfo.Color);
				Assert.Equal (SKEncodedInfoAlpha.Unpremul, codec.EncodedInfo.Alpha);
				Assert.Equal (8, codec.EncodedInfo.BitsPerComponent);
			}
		}

		[Fact]
		public void CanGetPixels ()
		{
			var stream = new SKFileStream (Path.Combine (PathToImages, "baboon.png"));
			using (var codec = SKCodec.Create (stream)) {
				var pixels = codec.Pixels;
				Assert.Equal (codec.Info.BytesSize, pixels.Length);
			}
		}

		[Fact]
		public void DecodePartialImage ()
		{
			// read the data here, so we can fake a throttle/download
			var path = Path.Combine (PathToImages, "baboon.png");
			var fileData = File.ReadAllBytes (path);
			SKColor[] correctBytes;
			using (var bitmap = SKBitmap.Decode (path)) {
				correctBytes = bitmap.Pixels;
			}

			int offset = 0;
			int maxCount = 1024 * 4;

			// the "download" stream needs some data
			var downloadStream = new MemoryStream ();
			downloadStream.Write (fileData, offset, maxCount);
			downloadStream.Position -= maxCount;
			offset += maxCount;

			using (var codec = SKCodec.Create (new SKManagedStream (downloadStream))) {
				var info = new SKImageInfo (codec.Info.Width, codec.Info.Height);

				// the bitmap to be decoded
				using (var incremental = new SKBitmap (info)) {

					// start decoding
					IntPtr length;
					var pixels = incremental.GetPixels (out length);
					var result = codec.StartIncrementalDecode (info, pixels, info.RowBytes);

					// make sure the start was successful
					Assert.Equal (SKCodecResult.Success, result);
					result = SKCodecResult.IncompleteInput;

					while (result == SKCodecResult.IncompleteInput) {
						// decode the rest
						int rowsDecoded = 0;
						result = codec.IncrementalDecode (out rowsDecoded);

						// write some more data to the stream
						maxCount = Math.Min (maxCount, fileData.Length - offset);
						downloadStream.Write (fileData, offset, maxCount);
						downloadStream.Position -= maxCount;
						offset += maxCount;
					}

					// compare to original
					Assert.Equal (correctBytes, incremental.Pixels);
				}
			}
		}

		[Fact]
		public void BitmapDecodesCorrectly ()
		{
			byte[] codecPixels;
			byte[] bitmapPixels;

			using (var codec = SKCodec.Create (new SKFileStream (Path.Combine (PathToImages, "baboon.png")))) {
				codecPixels = codec.Pixels;
			}

			using (var bitmap = SKBitmap.Decode (Path.Combine (PathToImages, "baboon.png"))) {
				bitmapPixels = bitmap.Bytes;
			}

			Assert.Equal (codecPixels, bitmapPixels);
		}

		[Fact]
		public void BitmapDecodesCorrectlyWithManagedStream ()
		{
			byte[] codecPixels;
			byte[] bitmapPixels;

			var stream = File.OpenRead (Path.Combine(PathToImages, "baboon.png"));
			using (var codec = SKCodec.Create (new SKManagedStream (stream))) {
				codecPixels = codec.Pixels;
			}

			using (var bitmap = SKBitmap.Decode (Path.Combine (PathToImages, "baboon.png"))) {
				bitmapPixels = bitmap.Bytes;
			}

			Assert.Equal (codecPixels, bitmapPixels);
		}
	}
}
