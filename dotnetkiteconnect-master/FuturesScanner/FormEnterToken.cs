using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FuturesScanner
{

    public partial class FormEnterToken : Form
    {
        public FormEnterToken()
        {
            InitializeComponent();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(textBox1.Text!=String.Empty)
            {
                FuturesScanner.DataObjects.Token.apitoken = textBox1.Text;
                this.Hide();
            }
        }

        private void FormEnterToken_Load(object sender, EventArgs e)
        {
            FuturesScanner.DataObjects.Token.apitoken = "";
        }
    }
}
