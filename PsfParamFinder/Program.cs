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
        public List<PsfParamater> psfparams;
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
    public struct PsfParamater
    {
        public string name;
        public byte[] value;
        public PsfParamater(string n, byte[] v)
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
            //int sig = sigpos("h:\\xdcc\\generic.exe");
            binvals("h:\\xdcc\\generic.exe");
        }
        /*
        static int sigpos(string file)
        {
            try
            {
                //FileStream fs = File.Open(file, FileMode.Open);
                StreamReader sr = new StreamReader(file, System.Text.Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
                int sig = psfexe.IndexOf("PSF_DRIVER_INFO:");
                
                //sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                return sig;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return -1;
        }
        */
        static string nullterm(string s, int index)
        {
            return s.Substring(index, s.IndexOf('\0', index) - index);
        }

        static void binvals(string file)
        {
            try
            {
                uint param, param2;
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
                //Console.WriteLine(ip.drivername);
                /*
                for (int i = 0; i < 40; i++)
                {
                    param = br.ReadUInt32();
                    Console.WriteLine("Parameter #{0} is {1:X} ({2:X})", i, param, param - ip.offset);
                }
                */
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
                fs.Seek(-8, SeekOrigin.Current);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
