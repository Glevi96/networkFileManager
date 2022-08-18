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
using pathForFile = System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Management;

namespace networkFileMananger
{
    class Program
    {
        public enum NET_API_STATUS : uint
    {
        NERR_Success = 0,
        /// <summary>
        /// This computer name is invalid.
        /// </summary>
        NERR_InvalidComputer = 2351,
        /// <summary>
        /// This operation is only allowed on the primary domain controller of the domain.
        /// </summary>
        NERR_NotPrimary = 2226,
        /// <summary>
        /// This operation is not allowed on this special group.
        /// </summary>
        NERR_SpeGroupOp = 2234,
        /// <summary>
        /// This operation is not allowed on the last administrative account.
        /// </summary>
        NERR_LastAdmin = 2452,
        /// <summary>
        /// The password parameter is invalid.
        /// </summary>
        NERR_BadPassword = 2203,
        /// <summary>
        /// The password does not meet the password policy requirements.
        /// Check the minimum password length, password complexity and password history requirements.
        /// </summary>
        NERR_PasswordTooShort = 2245,
        /// <summary>
        /// The user name could not be found.
        /// </summary>
        NERR_UserNotFound = 2221,
        ERROR_ACCESS_DENIED = 5,
        ERROR_NOT_ENOUGH_MEMORY = 8,
        ERROR_INVALID_PARAMETER = 87,
        ERROR_INVALID_NAME = 123,
        ERROR_INVALID_LEVEL = 124,
        ERROR_MORE_DATA = 234 ,
        ERROR_SESSION_CREDENTIAL_CONFLICT = 1219,
        /// <summary>
        /// The RPC server is not available. This error is returned if a remote computer was specified in
        /// the lpServer parameter and the RPC server is not available.
        /// </summary>
        RPC_S_SERVER_UNAVAILABLE = 2147944122, // 0x800706BA
        /// <summary>
        /// Remote calls are not allowed for this process. This error is returned if a remote computer was
        /// specified in the lpServer parameter and remote calls are not allowed for this process.
        /// </summary>
        RPC_E_REMOTE_DISABLED = 2147549468 // 0x8001011C
    }
    
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
        [DllImport("netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern int NetFileGetInfo(
          string servername,
          int fileid,
          int level,
          ref IntPtr bufptr
        );
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
        public static string getPath(string uncPath){
            try
            {
                // remove the "\\" from the UNC path and split the path
                uncPath = uncPath.Replace(@"\\", "");
                string[] uncParts = uncPath.Split(new char[] {'\\'}, StringSplitOptions.RemoveEmptyEntries);
                if (uncParts.Length < 2)
                    return "[UNRESOLVED UNC PATH: " + uncPath + "]";
                // Get a connection to the server as found in the UNC path
                ManagementScope scope = new ManagementScope(@"\\" + uncParts[0] + @"\root\cimv2");
                // Query the server for the share name
                SelectQuery query = new SelectQuery("Select * From Win32_Share Where Name = '" + uncParts[1] + "'");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);

            // Get the path
                string path = string.Empty;
                foreach (ManagementObject obj in searcher.Get())
                {
                    path = obj["path"].ToString();
                }

            // Append any additional folders to the local path name
                if (uncParts.Length > 2)
                {
                    for (int i = 2; i < uncParts.Length; i++)
                    path = path.EndsWith(@"\") ? path + uncParts[i] : path + @"\" + uncParts[i];
                }

                return path;
            }
            catch (Exception ex)
            {
                return "[ERROR RESOLVING UNC PATH: " + uncPath + ": "+ex.Message+"]";
            }
        }
        public static void ErrorDef(int err){
            if(err!=0){
                new ToastContentBuilder().AddArgument("action","viewConversation").AddArgument("conversationId",9813).AddText("Hiba!").AddText("A fájl nincs megnyitva a hálózaton.").Show();
            }else{
                new ToastContentBuilder().AddArgument("action","viewConversation").AddArgument("conversationId",9813).AddText("Siker!").AddText("A fájl sikeresen bezárva.").Show();
            }
        }
        public static void FindUNCPaths(){
            DriveInfo[] dis = DriveInfo.GetDrives();
            foreach(DriveInfo di in dis){
                if(di.DriveType == DriveType.Network){
                    DirectoryInfo dir = di.RootDirectory;
                    MessageBox.Show(GetUNCPath( dir.FullName.Substring(0,2)));
                }
            }
        }
        public static string GetUNCPath(string path)
        {
            if(path.StartsWith(@"\\")) 
            {
                return path;
            }

            ManagementObject mo = new ManagementObject();
            mo.Path = new ManagementPath( String.Format( "Win32_LogicalDisk='{0}'", path ) );

            // DriveType 4 = Network Drive
            if(Convert.ToUInt32(mo["DriveType"]) == 4 )
            {
                return Convert.ToString(mo["ProviderName"]);
            }
            else 
            {
                return path;
            }
        }
        static void Main(string[] args)
        {
            FindUNCPaths();
            CreateKey();
            string filePath=args[0];
            MessageBox.Show(filePath+" is the initial path for the file");
            string fullPath = pathForFile.Path.GetDirectoryName(args[0]);
            MessageBox.Show(fullPath+" is the path, after using GetDirectory method");
            //MessageBox.Show(args[0]+" is the intial value for the path");
            string basePath = getPath(filePath);
            //MessageBox.Show(basePath+" is the new value for the path");
            //splitThePath();
            const int MAX_PREFERRED_LENGTH = -1;
            int dwReadEntries;
            int dwTotalEntries;
            IntPtr pBuffer = IntPtr.Zero;
            FILE_INFO_3 pCurrent = new FILE_INFO_3();
            string serverName = "huszefsp01";
            //int netStatus = NetFileGetInfo(serverName,fileId,3,pBuffer);
            
            //int dwStatus = NetFileEnum("HUVALBATESTVM02", null, null, 3, ref pBuffer, MAX_PREFERRED_LENGTH, out dwReadEntries, out dwTotalEntries, IntPtr.Zero );
            
            
            //string basePath = @"C:\ShareTest\testTXT.txt";
            
            string username = null;
            int dwStatus = NetFileEnum(serverName, basePath, username, 3, ref pBuffer, MAX_PREFERRED_LENGTH, out dwReadEntries, out dwTotalEntries, IntPtr.Zero );
            if (dwStatus == 0) {
                for (int dwIndex=0; dwIndex < dwReadEntries; dwIndex++) {
                    IntPtr iPtr = new IntPtr(pBuffer.ToInt64() + (dwIndex * Marshal.SizeOf(pCurrent)));
                    
                    pCurrent = (FILE_INFO_3)Marshal.PtrToStructure(iPtr, typeof(FILE_INFO_3));
                    var thing = (NET_API_STATUS)NetFileClose(serverName,pCurrent.fi3_id);
                    MessageBox.Show(thing.ToString());
                    //string closer = Convert.ToString(NetFileClose(serverName,pCurrent.fi3_id));
                }
                NetApiBufferFree(pBuffer);
            }
        }
    }
}
