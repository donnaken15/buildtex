using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using DDS;
using System.IO;
using System.ComponentModel;
using System.Drawing.Design;
using System.ComponentModel.Design;
using Bit = System.BitConverter;
using System.Drawing.Imaging;

public class Zones
{
	public static uint Eswap(uint value)
	{
		return ((value & 0xFF) << 24) |
				((value & 0xFF00) << 8) |
				((value & 0xFF0000) >> 8) |
				((value & 0xFF000000) >> 24);
	}
	public static int Eswap(int value)
	{
		return unchecked((int)
				(((uint)(value & 0xFF) << 24) |
				((uint)(value & 0xFF00) << 8) |
				((uint)(value & 0xFF0000) >> 8) |
				((uint)(value & 0xFF000000) >> 24)));
	}
	public static ushort Eswap(ushort value)
	{
		return (ushort)(((value & 0xFF) << 8) |
			((value & 0xFF00) >> 8));
	}
	
	/// <summary>
	/// The image class used for creating and loading images in GH3.
	/// </summary>
	public class RawImg : IDisposable
	{
		const int _magic = 0x0A281100;
		public void Dispose()
		{
			Image.Dispose();
			rawimg = null;
		}
		public struct Head
		{
			public uint magic;
			public uint key; // null on .imgs, named on .tex
			public ushort w_scale, h_scale; // one of these scales the texture
			public ushort unk0; // 00 01
			public ushort w_clip, h_clip; // and one crops it by the looks of it
			public ushort unk1; // 00 01
			// these flags probably dont do anything on PC
			// they appear interchangeably on certain textures
			// but still consistent with DXT1 textures on some
			// with the 04 01 and 08 05
			public byte mipmaps, bpp, compression, unk2;
			public uint unk3;
			public uint off_start, size;
			// i dont remember
			// if these did anything either
			// but just be consistent anyway,
			// like with imggen
			public uint unk4;
		}
		public Head head;
		public ushort widthScale
		{
			get { return head.w_scale; }
			set { head.w_scale = value; }
		}
		public ushort widthClip
		{
			get { return head.w_clip; }
			set { head.w_clip = value; }
		}
		public ushort heightScale
		{
			get { return head.h_scale; }
			set { head.h_scale = value; }
		}
		public ushort heightClip
		{
			get { return head.h_clip; }
			set { head.h_clip = value; }
		}
		public byte[] rawimg;
		uint magic
		{
			get { return Eswap(Bit.ToUInt32(rawimg, 0)); }
		}
		bool isDDS
		{
			get { return magic == 0x44445320; }
		}
		public string ext
		{
			get
			{
				if (isDDS)
					return "dds";
				else
				{
					if ((magic & 0xFFFF0000) == 0x424d0000)
						return "bmp"; // because why
					switch (magic)
					{
						case 0x89504e47:
							return "png";
						case 0xffd8ffe0:
							return "jpg";
						default:
							return "";
					}
				}
			}
		}
		public Image Image
		{
			get
			{
				if (isDDS)
					return DDSImage.Load(rawimg).Images[0];
				return Image.FromStream(new MemoryStream(rawimg), true);
			}
			set
			{
				setHead();
				head.w_scale = (ushort)value.Width;
				head.h_scale = (ushort)value.Height;
				head.w_clip = (ushort)value.Width;
				head.h_clip = (ushort)value.Height;
				//ImageFormat fmt = img.RawFormat;
				try
				{
					value.Save(new MemoryStream(rawimg), value.RawFormat);
				}
				catch
				{
					// why
					value.Save(new MemoryStream(rawimg), ImageFormat.Png);
				}
				head.size = (uint)rawimg.Length;
			}
		}
		public QbKey Name
		{
			get { return QbKey.Create(head.key); }
			set { head.key = value.Crc; }
		}
		void setHead()
		{
			head.magic = _magic;
			head.unk0 = 1;
			head.unk1 = 1;
			head.mipmaps = 1;
			head.bpp = 8;
			head.compression = 5;
			head.off_start = 0x28;
		}
		public byte[] Save()
		{
			byte[] exported = new byte[0x28 + rawimg.Length];
			MemoryStream ms = new MemoryStream();
			BinaryWriter bw = new BinaryWriter(ms);
			bw.Write(Eswap(_magic));
			bw.Write(0);
			bw.Write(Eswap(head.w_scale));
			bw.Write(Eswap(head.h_scale));
			bw.Write(Eswap(head.unk0));
			bw.Write(Eswap(head.w_clip));
			bw.Write(Eswap(head.h_clip));
			bw.Write(Eswap(head.unk1));
			bw.Write(Eswap((byte)head.mipmaps));
			bw.Write(Eswap((byte)head.bpp));
			bw.Write(Eswap((byte)head.compression));
			bw.Write(Eswap(head.unk2));
			bw.Write(Eswap(head.unk3));
			bw.Write(Eswap(head.off_start));
			bw.Write(Eswap(head.size));
			bw.Write(Eswap(head.unk4));
			//bw.Write(rawimg);
			bw.Close();
			Array.Copy(ms.ToArray(), exported, 0x28);
			ms.Close();
			Array.Copy(rawimg, 0, exported, 0x28, rawimg.Length);
			return exported;
		}
		public RawImg(Image img)
		{
			setHead();
			head.w_scale = (ushort)img.Width;
			head.h_scale = (ushort)img.Height;
			head.w_clip = (ushort)img.Width;
			head.h_clip = (ushort)img.Height;
			//ImageFormat fmt = img.RawFormat;
			MemoryStream ms = new MemoryStream();
			img.Save(ms, img.RawFormat);
			rawimg = ms.ToArray();
			head.size = (uint)rawimg.Length;
		}
		public RawImg(DDSImage dds, byte[] data)
		{
			setHead();
			Image img = dds.Images[0];
			head.w_scale = (ushort)img.Width;
			head.h_scale = (ushort)img.Height;
			head.w_clip = (ushort)img.Width;
			head.h_clip = (ushort)img.Height;
			rawimg = data;
			head.size = (uint)data.Length;
		}
		public RawImg(byte[] img) // lazy copy again lol
		{
			if (Eswap(Bit.ToUInt32(img,0)) != _magic &&
				Eswap(Bit.ToUInt32(img,0)) != 0x0A281102 && // appears on fonts
				Eswap(Bit.ToUInt32(img,0)) != 0x0A280200) // kill me
			{
				Console.WriteLine("fail "+Eswap(Bit.ToUInt32(img,0)).ToString("X8") + ", expected "+_magic.ToString("X8"));
				head.magic = 0xBAADF00D; // indicate that this isn't usable
				// JUST SKIP THE FILE IF IT DOESN'T HAVE THIS MAGIC
				// FROM THE ZONES CONSTRUCTOR
				return;
			}
			head.key = Eswap(Bit.ToUInt32(img, 4));
			head.w_scale = Eswap(Bit.ToUInt16(img, 0x8));
			head.h_scale = Eswap(Bit.ToUInt16(img, 0xA));
			head.unk0 = Eswap(Bit.ToUInt16(img, 0xC));
			head.w_clip = Eswap(Bit.ToUInt16(img, 0xE));
			head.h_clip = Eswap(Bit.ToUInt16(img, 0x10));
			head.unk1 = Eswap(Bit.ToUInt16(img, 0x12));
			head.mipmaps = img[0x14];
			head.bpp = img[0x15];
			head.compression = img[0x16];
			head.unk2 = img[0x17];
			head.unk3 = Eswap(Bit.ToUInt32(img, 0x18));
			head.off_start = Eswap(Bit.ToUInt32(img, 0x1C));
			head.size = Eswap(Bit.ToUInt32(img, 0x20));
			if (head.size + 0x1C != img.Length)
				head.size = (uint)img.Length - 0x28; // i must die
			head.unk4 = Eswap(Bit.ToUInt32(img, 0x24));
			// what system is gonna run this with little endian
			rawimg = new byte[head.size];
			Array.Copy(img, head.off_start, rawimg, 0, head.size);
		}
		public static RawImg MakeFromRaw(string fname)
		{
			byte[] raw = File.ReadAllBytes(fname);
			if (Bit.ToUInt32(raw, 0) == 0x20534444)
			{
				// stupid
				return new RawImg(DDSImage.Load(fname), raw);
			}
			else if (Bit.ToUInt32(raw, 0) == Eswap(_magic))
			{
				return new RawImg(raw);
			}
			else
				return new RawImg(Image.FromFile(fname));
		}
		public void Export(string fname)
		{
			string a = Path.GetExtension(fname).Substring(1);
			if (a == ext)
				File.WriteAllBytes(fname, rawimg);
			else
			{
				// reconvert if extension is not the same
				ImageFormat fmt = ImageFormat.Png; // stupid C#
				switch (a)
				{
					case "dds":
						if (!isDDS)
							throw new Exception("Conversion to DXT is not supported. Thanks Shendare.");
						File.WriteAllBytes(fname, rawimg);
						break;
					case "jpg":
					case "jpeg":
						fmt = ImageFormat.Jpeg;
						break;
					case "bmp":
						fmt = ImageFormat.Bmp;
						break;
					case "tif":
					case "tiff":
						fmt = ImageFormat.Tiff;
						break;
					case "png":
					default:
						fmt = ImageFormat.Png;
						break;
				}
				Image.Save(fname, fmt);
			}
		}
	}

	public class GFX
	{
		public const uint magic = 0xFACECAA7;
		public const ushort const1 = 0x011C;
		private struct Head
		{
			public uint magic;
			public ushort const1; // 0x11C
			public ushort count;
			public uint start;
			// soy C# requiring ints
			// start + (count*44)         ??? "used in ef calculation" wtf
			public uint szrel, _FFFFFFFF, texLog, a28;
		}
		// INACCESSIBLE HOW
		private Head head;
		private List<RawImg> Images;
		public uint texLog {
			get { return head.texLog; }
			set { head.texLog = value; }
		}
		public uint startPos { get { return head.start; } }
		public uint szrel { get { return head.szrel; } }
		public int Count { get { return Images.Count; } }
		public List<RawImg>.Enumerator GetEnumerator() { return Images.GetEnumerator(); }
		public RawImg this[int i]
		{
			get { return Images[i]; }
			set {
				Images[i] = value;
			}
		}
		public RawImg this[uint c]
		{
			get { return IndexOf(c) > -1 ? this[IndexOf(c)] : null; }
		}
		public int IndexOf(uint i)
		{
			for (int j = 0; j < Count; j++)
			{
				if (this[j].Name.Crc == i)
					return j;
			}
			return -1;
		}
		public void UpdateHeader()
		{
			switch (head.texLog) // why
			{
				case 2:
					head.start = 0x68;
					break;
				case 8:
					head.start = 0xC38;
					break;
			}
			head.count = (ushort)Images.Count;
			head.szrel = head.start + (uint)(head.count*44);
		}
		public void Add(RawImg i)
		{
			Images.Add(i);
			UpdateHeader();
		}
		public void Remove(uint crc)
		{
			Images.RemoveAt(IndexOf(crc));
			UpdateHeader();
		}
		public GFX()
		{
			head = new Head();
			head.magic = 0xFACECAA7;
			head.const1 = 0x11C;
			head._FFFFFFFF = 0xFFFFFFFF;
			head.texLog = 8;
			head.a28 = 0x1C;
			Images = new List<RawImg>();
		}
	}

	public class Scene
	{
		public class Vec4
		{
			float[] _ = new float[4]; // Thanks Neversoft
			public float this[int i]
			{
				get { return _[i]; }
				set { _[i] = value; }
			}
			public Vec4() { }
			public Vec4(float x, float y, float z, float w)
			{
				_[0] = x;
				_[1] = y;
				_[2] = z;
				_[3] = w;
			}
			public override string ToString()
			{
				return
					'(' + _[0].ToString() + ','
						+ _[1].ToString() + ','
						+ _[2].ToString() + ','
						+ _[3].ToString() + ')';
			}
		}

		const byte MatVersion = 4;
		struct Head
		{
			// 0x00-0x20: random magic
			public byte ver, unk0;
			public ushort count;
			public int size;
			public uint unk1;
			public uint _FFFFFFFF;
		}
		Head head;
		public enum Blend : int // thx zed
		{
			Diffuse,
			Add,
			Sub,
			Blend,
			Mod,
			Brighten,
			Multiply,
			SrcPlusDst,
			Blend_AlphaDiffuse,
			SrcPlusDstMultInvAlpha,
			SrcMulDstPlusDst, // don't know if GH3 also has all of these
			DstSubSrcMulInvDst,
			DstMinusSrc
		}
			
		public /* new to this, and i already hate it */ class Mat
		{
			// thx zed
			public byte[] data; // doing stuff like this
			// and for RawImg to keep data intact
			// and since I don't know the use of
			// every single thing, so for those,
			// just do nothing with it and
			// resave zones with them unmodified
			//
			// writing this as i have not tested
			// or checked how mats load yet to
			// see if i winged writing this
			// ps: i basically did
			public const int toffset = 0xA0;
			public uint GetBase(int a) { return Eswap(Bit.ToUInt32(data, a)); }
			public int ssize
			{ // WHY IS THERE ALL THIS PADDING
				get { return Int(toffset + 0x24); }
				set {
					if (value < 0xEC)
						throw new InvalidDataException(
							"Internal material struct is too small to contain required values: " + value.ToString("X8"));
					CopyInt(value, toffset + 0x24);
					CopyInt(value, toffset + 0x44); // :|
					Array.Resize(ref data, value);
				}
			}
			public void CopyInt(uint v, int a) { Array.Copy(Bit.GetBytes(Eswap(v)), 0, data, a, 4); }
			public void CopyInt(int v, int a) { CopyInt((uint)v, a); }
			public void CopyFloat(float v, int a) { CopyInt(Bit.ToUInt32(Bit.GetBytes(v), 0), a); }
			public int Int(int a) { return (int)GetBase(a); }
			public uint UInt(int a) { return GetBase(a); }
			public QbKey Key(int a) { return QbKey.Create(GetBase(a)); }
			public float Float(int a) { return Bit.ToSingle(Bit.GetBytes(GetBase(a)), 0); }
			public void CopyVec(Vec4 v, int a)
			{
				for (int i = 0; i < 4; i++)
					CopyFloat(v[i], a + (i * 4));
			}
			// can i make these less repetitive
			public int prePropCount
			{
				get { return Int(toffset + 8); }
				set { CopyInt(value, toffset + 8); }
			}
			public int prePropOffset
			{
				get { return Int(toffset + 12); }
				set { CopyInt(value, toffset + 12); }
			}
			public Vec4 preProp // singular
			{
				get { return props(false, 0); }
				set { CopyVec(value, prePropOffset); }
			}
			public int postPropCount
			{
				get { return Int(toffset + 0x10); }
				set { CopyInt(value, toffset + 0x10); }
			}
			public int postPropOffset
			{
				get { return Int(toffset + 0x14); }
				set { CopyInt(value, toffset + 0x14); }
			}
			public Vec4 postProp // singular
			{
				get { return props(true, 0); }
				set { CopyVec(value, postPropOffset); }
			}
			public int texCount // ????????????
			{
				get { return Int(toffset + 0x18); }
				set { CopyInt(value, toffset + 0x18); }
			}
			public int texOffset
			{
				get { return Int(toffset + 0x1C); }
				set { CopyInt(value, toffset + 0x1C); }
			}
			public QbKey Texture
			{
				get { return Key(texOffset); }
				set { CopyInt((int)value.Crc, texOffset); }
			}
			public QbKey[] Textures
			{
				get {
					QbKey[] texs = new QbKey[texCount];
					for (int i = 0; i < texCount; i++)
						texs[i] = Key(texOffset + (texCount * 4));
					return texs;
				}
			}
			public Vec4 props(bool post, int i)
			{
				int propCount, propOffset;
				if (!post)
				{
					propCount = prePropCount;
					propOffset = prePropOffset;
				} else {
					propCount = postPropCount;
					propOffset = postPropOffset;
				}
				if (i >= propCount)
					throw new ArgumentOutOfRangeException(
						"Index went past the number of contained props: " +
						i + " > " + propCount + '.');
				return new Vec4(
					Float(propOffset + (i * 0x10) + 0),
					Float(propOffset + (i * 0x10) + 4),
					Float(propOffset + (i * 0x10) + 8),
					Float(propOffset + (i * 0x10) + 12));
			}
			public Vec4[] props_a(bool post, int c)
			{
				Vec4[] vl = new Vec4[c];
				for (int i = 0; i < c; i++)
					vl[i] = props(post, i);
				return vl;
			}
			public Blend Blend
			{
				get { return (Blend)Int(toffset + 0x38); }
				set { CopyInt((int)value, toffset + 0x38); }
			} // 3 / Blend
			public QbKey Material // sys_Gem2D_Green_sys_Gem2D_Green
			{
				get { return Key(0); }
				set { CopyInt(value.Crc, 0); }
			}
			public QbKey Shader // or template // ImmediateMode_(AlphaFade_)UI
			{
				get { return Key(toffset); }
				set { CopyInt(value.Crc, toffset); }
			}
			public Mat()
			{
				data = new byte[0x100];
				ssize = 0x100;
			}
		}
		List<Mat> mats = new List<Mat>();
		public int Count { get { return mats.Count; } }
		public List<Mat>.Enumerator GetEnumerator() { return mats.GetEnumerator(); }
		public Mat this[int i]
		{
			get { return mats[i]; }
		}
		public int matsSize()
		{
			int s = 0;
			for (int i = 0; i < mats.Count; i++)
				s += mats[i].ssize;
			return s + 0x10;
		}
		public void UpdateHeader()
		{
			head.count = (ushort)mats.Count;
			head.size = matsSize();
		}
		public int IndexOf(uint i)
		{
			for (int j = 0; j < Count; j++)
			{
				if (this[j].Material == i)
					return j;
			}
			return -1;
		}
		public int IndexOfTex(uint i)
		{
			for (int j = 0; j < Count; j++)
			{
				if (this[j].texCount > 0)
					if (this[j].UInt(this[j].texOffset) == i)
						return j;
			}
			return -1;
		}
		public void Add(Mat m)
		{
			mats.Add(m);
			UpdateHeader();
		}
		public void Remove(uint crc)
		{
			mats.RemoveAt(IndexOf(crc));
			UpdateHeader();
		}
		public Scene()
		{
			head.ver = MatVersion;
			head.unk0 = 0x10;
			head.unk1 = 0x10;
			head._FFFFFFFF = 0xFFFFFFFF;
		}
	}
}
