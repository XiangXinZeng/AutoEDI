using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoEDI
{
    class Program
    {
        #region appSettings
        private static string commandPath = ConfigurationManager.AppSettings["CommandPath"];

        private static string oper = ConfigurationManager.AppSettings["Operator"];
        private static string pass = ConfigurationManager.AppSettings["Password"];
        private static string comp = ConfigurationManager.AppSettings["Company"];
        private static string cpass = ConfigurationManager.AppSettings["CompanyPassword"];

        private static string invoicesFile = ConfigurationManager.AppSettings["InvoicesFile"];

        private static string connectionString = ConfigurationManager.ConnectionStrings["AutoEdi"].ToString();
        private static string sqlCommand = ConfigurationManager.AppSettings["SqlCommand"];
        #endregion

        static void Main(string[] args)
        {
            Console.WriteLine("Start...");
            List<Invoice> invoices;

            //get data from database
            try
            {
                invoices = GetInvoices();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database Access Error:" + ex.Message);
                return;
            }
            
            //write invoice code to file Invoices.lst
            try
            {
                WriteToFile(invoices.Select(i => i.InvCode).ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("File I/O Error:" + ex.Message);
                return;
            }

            //Call IMPAUT.EXE for each costomer code
            var customers = invoices.Select(i => i.Customer).Distinct().ToArray();
            foreach (var customer in customers)
            {
                try
                {
                    ExecuteCommand(customer);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error when calling IMPAUT.EXE :" + ex.Message);
                    return;
                }
            }
 
            Console.WriteLine("Processed Successfully! Press any key to exit.");
            Console.ReadLine();

        }

        private static void WriteToFile(string[] invCodes)
        {
            using (FileStream fs=File.OpenWrite(invoicesFile))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                { 
                    foreach (var inv in invCodes)
                    {
                        sw.WriteLine(inv);
                    }
                }
            }
        }

        private static List<Invoice> GetInvoices()
        {
            List<Invoice> invoices = new List<Invoice>();
            using (SqlConnection con = new SqlConnection(connectionString))
            using(SqlCommand cmd=new SqlCommand(sqlCommand,con))
            {
                con.Open();
                var rdr= cmd.ExecuteReader();
                while (rdr.Read())
                {
                    Invoice invoice = new Invoice
                    {
                        Customer = rdr.GetString(0),
                        InvCode=rdr.GetString(1)
                    };
                    invoices.Add(invoice);
                }
            }
             return invoices;
        }

        private static void ExecuteCommand(string customerCode)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = commandPath;
            processInfo.Arguments = $" /BASESEDIR=0 /HOST=MBCSQL /OPER={oper} /pass={pass} /comp={comp} /cpas={cpass} /prog=EDI810 /link={customerCode}:NRINA:NOREPORT:REPRINT:LIST";
            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process process = Process.Start(processInfo))
            {
                process.WaitForExit();
            }
        }
    }
}
