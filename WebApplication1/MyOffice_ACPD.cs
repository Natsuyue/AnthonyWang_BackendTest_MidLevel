using System;

namespace WebApplication1
{
    public class MyOffice_ACPD
    {
        public string ACPD_SID { get; set; } = string.Empty;

        public string? ACPD_Cname { get; set; }

        public string? ACPD_Ename { get; set; }

        public string? ACPD_Sname { get; set; }

        public string? ACPD_Email { get; set; }

        public byte? ACPD_Status { get; set; }

        public bool? ACPD_Stop { get; set; }

        public string? ACPD_StopMemo { get; set; }

        public string? ACPD_LoginID { get; set; }

        public string? ACPD_LoginPWD { get; set; }

        public string? ACPD_Memo { get; set; }

        public DateTime? ACPD_NowDateTime { get; set; }

        public string? ACPD_NowID { get; set; }

        public DateTime? ACPD_UPDDateTime { get; set; }

        public string? ACPD_UPDID { get; set; }
    }
}