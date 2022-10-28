using System;
using System.Collections.Generic;
using System.IO;
namespace PsfParamFinder
{
    [Serializable]
    public class InternalParams
    {
        public uint offset;
        public uint loadaddr;
        public uint entrypoint;
        public string drivername;
        public string exename;
        public uint crc;
        public uint jumppatch;
        public List<SongArea> blocks;
        public List<PsfParameter> psfparams;
        /*
        public InternalParams()
        {
            //exetext = 0;
            //loadaddr = 0;
            //entrypoint = 0;
            //drivername = "";
            //exename = "";
            //crc = 0;
            //jumppatch = 0;
            blocks = new List<SongArea>();
        }
        */
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
        public string name;
        public byte[] value;
        public PsfParameter(string n, byte[] v)
        {
            name = n;
            value = new byte[v.Length];
            v.CopyTo(value, 0);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            binvals("h:\\xdcc\\generic.exe");
        }
        static string nullterm(string s, int index)
        {
            return s.Substring(index, s.IndexOf('\0', index) - index);
        }

        static void binvals(string file)
        {
            try
            {
                uint param, param2;
                int param3;
                long postemp;
                FileStream fs = File.Open(file, FileMode.Open);
                BinaryReader br = new BinaryReader(fs);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
                int sig = psfexe.IndexOf("PSF_DRIVER_INFO:");
                InternalParams ip = new InternalParams();
                fs.Seek(24, SeekOrigin.Begin);
                ip.offset = br.ReadUInt32() - 2048;
                fs.Seek(sig + 16, SeekOrigin.Begin);
                ip.loadaddr = br.ReadUInt32() - ip.offset;
                ip.entrypoint = br.ReadUInt32() - ip.offset;
                param = br.ReadUInt32() - ip.offset;
                ip.drivername = nullterm(psfexe, (int)param);
                param = br.ReadUInt32() - ip.offset;
                ip.exename = nullterm(psfexe, (int)param);
                ip.crc = br.ReadUInt32();
                ip.jumppatch = br.ReadUInt32() - ip.offset;
                SongArea sa;
                param = br.ReadUInt32();
                param2 = br.ReadUInt32();
                sa.addr = param - ip.offset;
                sa.size = param2;
                ip.blocks = new List<SongArea>();
                while (param != 0 && param2 != 0)
                {
                    ip.blocks.Add(sa);
                    param = br.ReadUInt32();
                    param2 = br.ReadUInt32();
                    sa.addr = param - ip.offset;
                    sa.size = param2;
                }
                fs.Seek(-4, SeekOrigin.Current);
                param = br.ReadUInt32() - ip.offset;
                param2 = br.ReadUInt32() - ip.offset;
                param3 = br.ReadInt32();
                postemp = fs.Position;
                fs.Seek(param2, SeekOrigin.Begin);

                while (param != 0 && param2 != 0 && param3 != 0)
                {
                    PsfParameter pp = new PsfParameter(nullterm(psfexe, (int)param), br.ReadBytes(param3));
                    fs.Seek(postemp, SeekOrigin.Begin);
                    Console.WriteLine("Name: {0} Number of Bytes: {1} Value: {2}", pp.name, param3, BitConverter.ToString(pp.value));
                    if (param3 == 4)
                    {
                        Console.WriteLine("OFFSET VALUE: {0:X}", BitConverter.ToUInt32(pp.value) - ip.offset);
                    }
                    param = br.ReadUInt32() - ip.offset;
                    param2 = br.ReadUInt32() - ip.offset;
                    param3 = br.ReadInt32();
                    postemp = fs.Position;
                    fs.Seek(param2, SeekOrigin.Begin);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
