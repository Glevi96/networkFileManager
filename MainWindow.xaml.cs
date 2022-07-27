using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Toolkit.Uwp.Notifications;

namespace networkFileMananger
{
    class Program
    {
        public static string filePath = "";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct FILE_INFO_3
        {
            public int fi3_id;
            public int fi3_permission;
            public int fi3_num_locks;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string fi3_pathname;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string fi3_username;
        }
        [DllImport("netapi32.dll",SetLastError=true, CharSet = CharSet.Unicode)]
        public static extern int NetFileClose(string serverName, int fileId);
        [DllImport("netapi32.dll",SetLastError=true, CharSet = CharSet.Unicode)]
        static extern int NetFileEnum(
            string serverName,
            string basePath,
            string username,
            int level,
            ref IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            IntPtr resume_handle
        );
        [DllImport("Netapi32.dll", SetLastError=true)]
        static extern int NetApiBufferFree(IntPtr Buffer);
        static void CreateKey(){
            RegistryKey rootKey = Registry.CurrentUser;
            RegistryKey rkSubKey = Registry.CurrentUser.OpenSubKey("Software\\Classes\\*\\shell\\CloseNetWorkFile", false);
            if(rkSubKey == null){
                RegistryKey subKey = rootKey.CreateSubKey("Software\\Classes\\*\\shell\\CloseNetWorkFile");
                subKey.SetValue("", "A fájl bezárása a hálózaton");
                subKey.CreateSubKey("command").SetValue("", Application.ExecutablePath + " %1");
                subKey.Close();
                rootKey.Close();
            }
        }
        public static string[] splitPath;
        static void splitThePath(){
            splitPath = filePath.Split(@"\");
            splitPath = splitPath.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            rebuildBasePath();
        }
        static void rebuildBasePath(){
            for(int i = 1;i<splitPath.Count();i++){
                basePath+=splitPath[i]+@"\";
            }
            basePath = basePath.Remove(basePath.Length-1);
            basePath = @"C:\"+basePath;
            //System.Console.WriteLine(basePath+" is the rebuilt path for the file");
        }
        public static string basePath;
        static void Main(string[] args)
        {
            CreateKey();
            filePath=args[0];
            splitThePath();
            const int MAX_PREFERRED_LENGTH = -1;
            
            int dwReadEntries;
            int dwTotalEntries;
            IntPtr pBuffer = IntPtr.Zero ;
            FILE_INFO_3 pCurrent = new FILE_INFO_3();
            
            //int dwStatus = NetFileEnum("HUVALBATESTVM02", null, null, 3, ref pBuffer, MAX_PREFERRED_LENGTH, out dwReadEntries, out dwTotalEntries, IntPtr.Zero );
            
            string serverName = splitPath[0];
            //string basePath = @"C:\ShareTest\testTXT.txt";
            
            string username = null;

            int dwStatus = NetFileEnum(serverName, basePath, username, 3, ref pBuffer, MAX_PREFERRED_LENGTH, out dwReadEntries, out dwTotalEntries, IntPtr.Zero );
            if(dwReadEntries>1){
                new ToastContentBuilder().AddArgument("action","viewConversation").AddArgument("conversationId",9813).AddText("Siker!").AddText("A fájl sikeresen bezárva.").Show();
            }else{
                new ToastContentBuilder().AddArgument("action","viewConversation").AddArgument("conversationId",9813).AddText("Hiba!").AddText("A fájl nincs megnyitva a hálózaton.").Show();
            }
            if (dwStatus == 0) {
                for (int dwIndex=0; dwIndex < dwReadEntries; dwIndex++) {
                    IntPtr iPtr = new IntPtr(pBuffer.ToInt64() + (dwIndex * Marshal.SizeOf(pCurrent)));
                    
                    pCurrent = (FILE_INFO_3) Marshal.PtrToStructure(iPtr, typeof(FILE_INFO_3));

                    /*
                    Console.WriteLine("dwIndex={0}", dwIndex);
                    Console.WriteLine("    id={0}", pCurrent.fi3_id );
                    Console.WriteLine("    num_locks={0}", pCurrent.fi3_num_locks );
                    Console.WriteLine("    pathname={0}", pCurrent.fi3_pathname );
                    Console.WriteLine("    permission={0}", pCurrent.fi3_permission );
                    Console.WriteLine("    username={0}", pCurrent.fi3_username  );
                    */

                     NetFileClose(serverName,pCurrent.fi3_id);
                }
                NetApiBufferFree(pBuffer);
            }
        }
    }
}
