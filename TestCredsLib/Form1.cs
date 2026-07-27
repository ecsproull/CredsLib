using CredsLib;
using System;
using System.Windows.Forms;
namespace TestCredsLib
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Test_Click(object sender, EventArgs e)
        {
            this.Output.Text = CredentialManager.GetConnectionString(this.UserName.Text, this.DatabaseName.Text, this.ConnectionStringName.Text); 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //exit the application
        }
    }
}
