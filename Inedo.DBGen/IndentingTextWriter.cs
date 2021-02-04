using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class IndentingTextWriter : TextWriter
    {
        private static readonly UTF8Encoding utf8 = new UTF8Encoding(false);
        private readonly TextWriter writer;
        private bool newline = true;

        public IndentingTextWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        public override Encoding Encoding => utf8;

        public int Indent { get; set; }

        public override void Write(char value)
        {
            if (this.newline)
            {
                this.writer.Write(new string(' ', this.Indent * 4));
                this.newline = false;
            }

            this.writer.Write(value);
        }
        public override void WriteLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                this.WriteLine();
                return;
            }

            foreach (var line in Regex.Split(value, @"\r?\n"))
            {
                if (this.newline)
                {
                    this.writer.Write(new string(' ', this.Indent * 4));
                    this.newline = false;
                }

                this.writer.WriteLine(line);
                this.newline = true;
            }
        }
        public override void WriteLine()
        {
            this.writer.WriteLine();
            this.newline = true;
        }
        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (Regex.IsMatch(value, @"\r?\n"))
            {
                var lines = Regex.Split(value, @"\r?\n");
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (this.newline)
                    {
                        this.writer.Write(new string(' ', this.Indent * 4));
                        this.newline = false;
                    }

                    this.writer.WriteLine(lines[i]);
                    this.newline = true;
                }

                if (this.newline)
                {
                    this.writer.Write(new string(' ', this.Indent * 4));
                    this.newline = false;
                }

                this.writer.Write(lines[lines.Length - 1]);
            }
            else
            {
                if (this.newline)
                {
                    this.writer.Write(new string(' ', this.Indent * 4));
                    this.newline = false;
                }

                this.writer.Write(value);
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.writer.Dispose();
            base.Dispose(disposing);
        }
    }
}
