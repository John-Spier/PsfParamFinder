using System;
using System.Collections.Generic;
using System.IO;
//using System.Buffers.Binary;
//using ComponentAce.Compression.Libs.zlib;
using Ionic.Zlib;
using Force.Crc32;


//using System.Runtime.Serialization.Formatters.Binary;
//use ipaddress fucntions to change endians?
namespace PsfParamFinder
{
    public enum PsfTypes
    {
        EXE,
        PSF,
        MINIPSF
    }
    public class PsfFile
    {
        public uint segment;
        public bool modified;
        public string filename;
        public byte[] headersect;
        public uint start;
        public uint end;
        public uint crc;
        public byte[] reserved_area;
        public byte[] tags;
    }
    public class PsfTable
    {
        public PsfTypes ftype;
        public byte[] ram;
        public List<PsfFile> minipsfs;
    }
    [Serializable]
    public class InternalParams
    {
        public uint offset;
        public int sig;
        public uint loadaddr;
        public uint entrypoint;
        public string drivername;
        public string exename;
        public uint crc;
        public uint jumppatch;
        public List<SongArea> blocks;
        public Dictionary<string,PsfParameter> psfparams;
    }
    [Serializable]
    public struct SongArea
    {
        public uint addr;
        public uint size;
    }

    [Serializable]
    public struct PsfParameter
    {
        
        public int loc;
        public byte[] value;
        public PsfParameter(int l, byte[] v)
        {
            loc = l;
            value = new byte[v.Length];
            v.CopyTo(value, 0);
        }
    }

    [Serializable]
    public struct ParamsV1
    {
        public short SeqNum;
        public short Version;
        public short MvolL;
        public short MvolR;
        public short VolL;
        public short VolR;
        public short RvolL;
        public short RvolR;
        public short RdepthL;
        public short RdepthR;
        public short Rdelay;
        public short Rmode;
        public short Rfeedback;
        public int TickMode;
        public char SeqFlags;
        public char SeqType;
    }

    class Program
    {
        static void Main(string[] args)
        {
            ParamsV1 savepar_test = new ParamsV1();
            savepar_test.SeqNum = 0;
            savepar_test.Version = 1;
            savepar_test.MvolL = 64;
            savepar_test.MvolR = 64;
            savepar_test.VolL = 64;
            savepar_test.VolR = 64;
            savepar_test.RvolL = 64;
            savepar_test.RvolR = 64;
            savepar_test.RdepthL = 64;
            savepar_test.RdepthR = 64;
            savepar_test.Rdelay = 64;
            savepar_test.Rmode = 3;
            savepar_test.Rfeedback = 64;
            savepar_test.TickMode = 0x1000;
            savepar_test.SeqFlags = (char)0x00;
            savepar_test.SeqType = (char)0x00;

            /*
            savepar_test.SeqNum = 0x10FE;
            savepar_test.Version = 0x11FF;
            savepar_test.MvolL = 65;
            savepar_test.MvolR = 64;
            savepar_test.VolL = 66;
            savepar_test.VolR = 67;
            savepar_test.RvolL = 68;
            savepar_test.RvolR = 69;
            savepar_test.RdepthL = 63;
            savepar_test.RdepthR = 62;
            savepar_test.Rdelay = 61;
            savepar_test.Rmode = 30;
            savepar_test.Rfeedback = 6;
            savepar_test.TickMode = 0x1001;
            savepar_test.SeqFlags = (char)0xA;
            savepar_test.SeqType = (char)0xB;
            */


            FileStream spar = File.OpenWrite(args[0]);
            //BinaryFormatter binform = new BinaryFormatter();
            //binform.Serialize(spar, savepar_test);
            BinaryWriter nopointer = new BinaryWriter(spar, System.Text.Encoding.UTF8);
            nopointer.Write(savepar_test.SeqNum);
            nopointer.Write(savepar_test.Version);
            nopointer.Write(savepar_test.MvolL);
            nopointer.Write(savepar_test.MvolR);
            nopointer.Write(savepar_test.VolL);
            nopointer.Write(savepar_test.VolR);
            nopointer.Write(savepar_test.RvolL);
            nopointer.Write(savepar_test.RvolR);
            nopointer.Write(savepar_test.RdepthL);
            nopointer.Write(savepar_test.RdepthR);
            nopointer.Write(savepar_test.Rdelay);
            nopointer.Write(savepar_test.Rmode);
            nopointer.Write(savepar_test.Rfeedback);
            nopointer.Write(savepar_test.TickMode);
            nopointer.Write(savepar_test.SeqFlags);
            nopointer.Write(savepar_test.SeqType);
            nopointer.Close();
            spar.Close();
            return;






        }

        static void ParamsCatalog(string dir, bool allexe, StreamWriter outstream, bool drvout, bool paramout)
        {
            //StreamWriter con = new StreamWriter(outstream);
            Dictionary<string, PsfParameter> psfParameters = new Dictionary<string, PsfParameter>();
            Dictionary<string, InternalParams> psfDrivers = new Dictionary<string, InternalParams>();
            PsfParameter tp;
            InternalParams td;
            if (allexe)
            {
                DirParams(dir, "*.exe", false, psfParameters, psfDrivers);
            }
            else
            {
                foreach (string d in Directory.EnumerateDirectories(dir))
                {
                    DirParams(d, "*.exe", true, psfParameters, psfDrivers);
                }
            }
            if (paramout)
            {
                foreach (string k in psfParameters.Keys)
                {
                    if (psfParameters.TryGetValue(k, out tp))
                    {
                        outstream.WriteLine("{0} ({1})", k, tp.value.Length);
                    }

                }
            }
            if (drvout)
            {
                Console.WriteLine("\n\n\n=====DRIVERS=====\n\n\n");


                foreach (string l in psfDrivers.Keys)
                {
                    if (psfDrivers.TryGetValue(l, out td))
                    {
                        Console.WriteLine(l);
                        Console.WriteLine(td.drivername);
                        foreach (string m in td.psfparams.Keys)
                        {
                            if (psfParameters.TryGetValue(m, out tp))
                            {
                                Console.WriteLine("{0} ({1})", m, tp.value.Length);
                            }

                        }
                    }
                    Console.WriteLine("\n\n\n");

                }
            }
        }

        static void DirParams(string d, string pattern, bool onefilebreak, Dictionary<string, PsfParameter> parameters, Dictionary<string, InternalParams> drivers)
        {
            PsfTable mem;
            foreach (string f in Directory.EnumerateFiles(d, pattern, SearchOption.AllDirectories))
            {
                //Console.WriteLine(f);
                mem = LoadFile(f);
                MemoryStream fstream = new MemoryStream(mem.ram);
                InternalParams testpar = binvals(fstream);
                PsfParameter pp;
                string drvname;
                if (testpar != null)
                {
                    drvname = testpar.drivername;
                    testpar.drivername = f;
                    drivers.TryAdd(drvname, testpar);
                    foreach (string sp in testpar.psfparams.Keys)
                    {

                        if (testpar.psfparams.TryGetValue(sp, out pp))
                        {
                            parameters.TryAdd(sp, pp);
                        }
                    }

                }
                if (onefilebreak)
                {
                    return;
                }
            }
        }
        static string nullterm(string s, int index)
        {
            return s.Substring(index, s.IndexOf('\0', index) - index);
        }

        static string[] psflibs(BinaryReader br, int tagpos) //fix to match with spec - while loop?
        {
            try
            {
                List<string> liblines = new List<string>();
                StreamReader sr = new StreamReader(br.BaseStream);
                string lib = "";
                if (tagpos < 0)
                {
                    br.BaseStream.Seek(4, SeekOrigin.Begin);
                    uint rsize = br.ReadUInt32();
                    uint psize = br.ReadUInt32();
                    tagpos = (int)(16 + psize + rsize);
                }
                if (tagpos > (br.BaseStream.Length - 5))
                {
                    return new string[0];
                }
                br.BaseStream.Seek(tagpos, SeekOrigin.Begin);
                uint tagsig = br.ReadUInt32();

                if (tagsig == 0x4741545B && br.ReadByte() == 0x5D)
                {
                    while (sr.Peek() >= 0)
                    {
                        try
                        {
                            lib = sr.ReadLine();
                            if (lib.ToLowerInvariant().StartsWith("_lib"))
                            {
                                liblines.Add(lib);
                            }
                        }
                        catch (Exception tx)
                        {
                            Console.Error.WriteLine("Exception: {0}", tx.Message);
                            Console.Error.WriteLine("{0} was not a valid tag line", lib);
                        }

                    }
                }
                liblines.Sort();
                List<string> libs = new List<string>();

                foreach (string ls in liblines)
                {
                    try
                    {
                        libs.Add(ls.Split('=', StringSplitOptions.RemoveEmptyEntries)[1]);
                    }
                    catch (Exception lx)
                    {
                        Console.Error.WriteLine("{0} was not a valid library", ls);
                        Console.Error.WriteLine("Exception: {0}", lx.Message);
                    }
                }
                return libs.ToArray();
            }
            catch (Exception mx)
            {
                Console.Error.WriteLine("Tag Exception: {0}", mx.Message);
            }
            return new string[0];
        }

        static PsfTable LoadFile(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);
            uint ftype = br.ReadUInt32();
            PsfTable pt = null;
            switch (ftype)
            {
                case 0x01465350: //PSF
                    string[] pl = psflibs(br, -1);
                    if (pl.Length > 0)
                    {
                        fs.Dispose();
                        pt = LoadMiniPsf(filename);
                    }
                    else
                    {
                        pt = LoadPsf(br);
                        pt.minipsfs[0].filename = filename;
                        fs.Dispose();
                    }

                    break;
                case 0x582D5350: //PSX EXE
                    pt = LoadExe(br);
                    fs.Dispose();
                    break;
                default:
                    Console.Error.WriteLine("{0} is not a readable PSF or EXE file!", filename);
                    break;
                    
            }
            
            return pt;
        }

        

        static PsfTable LoadMiniPsf(string filename)
        {
            PsfTable ptab = new PsfTable();
            ptab.ftype = PsfTypes.MINIPSF;
            ptab.ram = new byte[0x200000];
            ptab.minipsfs = new List<PsfFile>();
            LoadPsfFile(filename, ptab);
            uint lowest = uint.MaxValue;
            uint highest = uint.MinValue;
            foreach (PsfFile se in ptab.minipsfs)
            {
                if (se.start < lowest)
                {
                    lowest = se.start;
                }
                if (se.end > highest)
                {
                    highest = se.end;
                }
            }
            
            byte[] mem = new byte[(highest - lowest) + 2048];
            Array.Copy(ptab.ram, lowest, mem, 2048, highest - lowest);
            Array.Copy(ptab.minipsfs[0].headersect, mem, 2048);
            byte[] size = BitConverter.GetBytes(highest - lowest);
            byte[] start = BitConverter.GetBytes(lowest + (ptab.minipsfs[0].segment * 0x20000000));
            Array.Copy(start, 0, mem, 0x18, 4);
            Array.Copy(size, 0, mem, 0x1C, 4);
            foreach (PsfFile psf in ptab.minipsfs)
            {
                psf.start -= lowest - 2048;
            }
            ptab.ram = mem;
            return ptab;
        }

        static bool LoadPsfFile (string fn, PsfTable tab)
        {
            try
            {
                FileStream file = new FileStream(fn, FileMode.Open);
                BinaryReader binary = new BinaryReader(file);
                PsfFile info = new PsfFile();
                byte[] tempram = new byte[0x200000];
                binary.BaseStream.Seek(4, SeekOrigin.Begin);
                int rsize = binary.ReadInt32();
                int psize = binary.ReadInt32();

                string[] libraries = psflibs(binary, 16 + psize + rsize);
                foreach (string l in libraries)
                {
                    LoadPsfFile(l, tab);
                }

                binary.BaseStream.Seek(12, SeekOrigin.Begin);
                info.crc = binary.ReadUInt32();
                info.modified = false;
                info.reserved_area = binary.ReadBytes(rsize);
                ZlibStream zlib = new ZlibStream(binary.BaseStream, CompressionMode.Decompress);
                int bytesread = zlib.Read(tempram, 0, 0x200000); 
                info.headersect = new byte[2048];
                Array.Copy(tempram, info.headersect, 2048);

                info.segment = BitConverter.ToUInt32(info.headersect, 24) / 0x20000000;
                info.start = BitConverter.ToUInt32(info.headersect, 24) % 0x20000000;
                info.end = info.start + (uint)(bytesread - 2048);

                Array.Copy(tempram, 2048, tab.ram, info.start, bytesread - 2048);
                binary.BaseStream.Seek(16 + rsize, SeekOrigin.Begin);
                tempram = binary.ReadBytes(psize);
                if (Crc32Algorithm.Compute(tempram) != info.crc)
                {
                    Console.Error.WriteLine("{0}: Wrong CRC!", fn);
                }
                info.filename = fn;
                tab.minipsfs.Add(info);
                binary.Dispose();
                file.Dispose();
                return true;
            }
            catch (Exception px)
            {
                Console.Error.WriteLine("File {0} exception: {1}", fn, px.Message);

            }
            return false;
        }

        static PsfTable LoadPsf(BinaryReader f)
        {
            PsfTable ptab = new PsfTable();
            ptab.ftype = PsfTypes.PSF;
            ptab.minipsfs = new List<PsfFile>();
            PsfFile info = new PsfFile();
            byte[] tempram = new byte[0x200000];
            f.BaseStream.Seek(4, SeekOrigin.Begin);
            int rsize = f.ReadInt32();
            int psize = f.ReadInt32();
            info.crc = f.ReadUInt32();
            info.modified = false;
            info.reserved_area = f.ReadBytes(rsize);
            ZlibStream zlib = new ZlibStream(f.BaseStream, CompressionMode.Decompress);
            int bytesread = zlib.Read(tempram, 0, 0x200000);
            info.headersect = new byte[2048];
            Array.Copy(tempram, info.headersect, 2048);
            ptab.ram = new byte[bytesread];
            Array.Copy(tempram, ptab.ram, bytesread);
            f.BaseStream.Seek(16 + psize + rsize, SeekOrigin.Begin);
            info.tags = f.ReadBytes((int)f.BaseStream.Length - (int)f.BaseStream.Position);
            f.BaseStream.Seek(16 + rsize, SeekOrigin.Begin);
            tempram = f.ReadBytes(psize);
            if (Crc32Algorithm.Compute(tempram) != info.crc)
            {
                Console.Error.WriteLine("Wrong CRC!");
            }
            info.segment = BitConverter.ToUInt32(info.headersect, 24) / 0x20000000;
            info.start = 2048;
            info.end = (uint)(bytesread - 2048) + info.start;
            ptab.minipsfs.Add(info);
            return ptab;
        }

        static PsfTable LoadExe(BinaryReader b)
        {
            
            b.BaseStream.Seek(0, SeekOrigin.Begin);
            PsfTable psf = new PsfTable();
            psf.ftype = PsfTypes.EXE;
            psf.ram = b.ReadBytes((int)b.BaseStream.Length);
            return psf;
        }
        static InternalParams binvals(Stream fs) //rewrite this without streams if you ever get around to it
        {
            try
            {
                uint param, param2, ipoffset, tparam, tparam2;
                int param3;
                long postemp;
                
                BinaryReader br = new BinaryReader(fs);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
                InternalParams ip = new InternalParams();
                ip.sig = psfexe.IndexOf("PSF_DRIVER_INFO:");
                fs.Seek(24, SeekOrigin.Begin);
                ip.offset = br.ReadUInt32() - 2048;
                ipoffset = ip.offset % 0x20000000;
                fs.Seek(ip.sig + 16, SeekOrigin.Begin);
                ip.loadaddr = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.entrypoint = br.ReadUInt32() % 0x20000000 - ipoffset;
                param = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.drivername = nullterm(psfexe, (int)param);
                param = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.exename = nullterm(psfexe, (int)param);
                ip.crc = br.ReadUInt32();
                ip.jumppatch = br.ReadUInt32() % 0x20000000 - ipoffset;
                SongArea sa;
                param = br.ReadUInt32();
                param2 = br.ReadUInt32();
                sa.addr = param % 0x20000000 - ipoffset;
                sa.size = param2;
                ip.blocks = new List<SongArea>();
                while (param != 0 && param2 != 0)
                {
                    ip.blocks.Add(sa);
                    param = br.ReadUInt32();
                    param2 = br.ReadUInt32();
                    sa.addr = param % 0x20000000 - ipoffset;
                    sa.size = param2;
                }
                ip.psfparams = new Dictionary<string, PsfParameter>();
                fs.Seek(-4, SeekOrigin.Current);
                tparam = br.ReadUInt32();
                tparam2 = br.ReadUInt32();
                param = tparam % 0x20000000 - ipoffset;
                param2 = tparam2 % 0x20000000 - ipoffset;
                param3 = br.ReadInt32();
                postemp = fs.Position;
                while (tparam != 0 && tparam2 != 0 && param3 != 0)
                {
                    fs.Seek(param2, SeekOrigin.Begin);
                    PsfParameter pp = new PsfParameter((int)param, br.ReadBytes(param3));
                    ip.psfparams.Add(nullterm(psfexe, (int)param), pp);
                    fs.Seek(postemp, SeekOrigin.Begin);
                    tparam = br.ReadUInt32();
                    tparam2 = br.ReadUInt32();
                    param = tparam % 0x20000000 - ipoffset;
                    param2 = tparam2 % 0x20000000 - ipoffset;
                    param3 = br.ReadInt32();
                    postemp = fs.Position;
                    
                }
                return ip;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
            return null;
        }
    }
}
