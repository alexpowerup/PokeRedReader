using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;
using WpfMath;
using System.Windows.Media.Imaging;
using CSharpMath.SkiaSharp;

namespace PokeRedReader
{
    public partial class LoadedRom : Form
    {
        private ROMAccessor ROM;

        public LoadedRom()
        {
            InitializeComponent();

            Shown += LoadedROM_Shown;
        }

        private void LoadedROM_Shown(Object sender, EventArgs e)
        {
            LoadROM();
        }

        public void LoadROM()
        {
            var ROMFileDialog = new OpenFileDialog();
            if (ROMFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.ROM = new ROMAccessor(ROMFileDialog.FileName);
                this.reloadMonListFriendlyOrdered();
            } else
            {
                Application.Exit();
            }
        }

        public void reloadMonList()
        {
            //Get dex number for every pokémon
            var mons = new byte[0xFF + 1];
            for (int i = 0; i < mons.Length; i++)
            {
                mons[i] = this.ROM.GetDexFromId((byte)i);
            }

            //Create string array with every option
            var textList = new String[mons.Length];
            for (int i = 0; i < textList.Length; i++)
            {
                textList[i] = String.Format("#{0} - {1} (ID {2})", mons[i], this.ROM.GetMonName((byte)i), i);
            }

            //Clear list
            MonListComboBox.Items.Clear();

            //Fill list
            for (int i = 0; i < textList.Length; i++)
            {
                var item = new ComboboxItem();
                item.Value = i;
                item.Text = textList[i];
                MonListComboBox.Items.Add(item);
            }
        }

        public void reloadMonListFriendlyOrdered()
        {
            var legalMons = new Dictionary<byte, byte>();
            var missingNos = new Dictionary<byte, byte>();
            var glitchMons = new Dictionary<byte, byte>();

            byte dex;
            for (int i = 0; i <= 0xFF; i++)
            {
                dex = this.ROM.GetDexFromId((byte)i);

                if (i >=1 && i <= 190)
                {
                    if (dex == 0) missingNos.Add((byte)i, dex);
                    else legalMons.Add((byte)i, dex);
                } else
                {
                    glitchMons.Add((byte)i, dex);
                }
            }

            //Sort legal mons by dex number
            var legalMonsSorted = legalMons.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            //Create full ordered list
            var textList = new List<ComboboxItem>();
            foreach (KeyValuePair<byte, byte> entry in legalMonsSorted)
            {
                var tmp = new ComboboxItem();
                tmp.Text = String.Format("#{0} - {1} (ID {2})", entry.Value, this.ROM.GetMonName(entry.Key), entry.Key);
                tmp.Value = entry.Key;
                textList.Add(tmp);
            }

            foreach (KeyValuePair<byte, byte> entry in missingNos)
            {
                var tmp = new ComboboxItem();
                tmp.Text = String.Format("#{0} - {1} (ID {2})", entry.Value, this.ROM.GetMonName(entry.Key), entry.Key);
                tmp.Value = entry.Key;
                textList.Add(tmp);
            }

            foreach (KeyValuePair<byte, byte> entry in glitchMons)
            {
                var tmp = new ComboboxItem();
                tmp.Text = String.Format("#{0} - {1} (ID {2})", entry.Value, this.ROM.GetMonName(entry.Key), entry.Key);
                tmp.Value = entry.Key;
                textList.Add(tmp);
            }

            //Clear list
            MonListComboBox.Items.Clear();

            //Fill list
            foreach (ComboboxItem entry in textList)
            {
                MonListComboBox.Items.Add(entry);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var obj = (ComboBox)sender;

            var comboboxItem = (ComboboxItem)obj.SelectedItem;

            byte id = (byte)comboboxItem.Value;

            byte dex = this.ROM.GetDexFromId(id);

            //Base stats
            var baseStats = this.ROM.GetMonBaseStats(dex);

            //Sprites
            var decompressor = new SpriteDecompressor(this.ROM);

            //Color palette
            var spritePalette = this.ROM.GetMonPalette(dex);

            //Front sprite
            decompressor.DecompressFront(id);
            var frontSprite = decompressor.RenderSprite(FrontSpriteColorCheckBox.CheckState == CheckState.Checked ? spritePalette : null);
            this.FrontSpritePictureBox.Image = frontSprite;

            //Back sprite
            decompressor.DecompressBack(id);
            var backSprite = decompressor.RenderSprite(BackSpriteColorCheckBox.CheckState == CheckState.Checked ? spritePalette : null);
            this.BackSpritePictureBox.Image = backSprite;

            //Stats
            BaseHPTextBox.Text = baseStats.HP.ToString();
            BaseATKTextBox.Text = baseStats.ATK.ToString();
            BaseDEFTextBox.Text = baseStats.DEF.ToString();
            BaseSPCTextBox.Text = baseStats.SPC.ToString();
            BaseSPDTextBox.Text = baseStats.SPD.ToString();

            //Stats 2
            DexNumberTextBox.Text = baseStats.DexNumber.ToString();
            Type1TextBox.Text = this.ROM.GetType(baseStats.Type1);
            Type2TextBox.Text = (baseStats.Type1 == baseStats.Type2) ? "-----" : this.ROM.GetType(baseStats.Type2);
            CatchRateTextBox.Text = baseStats.CatchRate.ToString();
            ExpYieldTextBox.Text = baseStats.BaseExp.ToString();

            //Pokédex info
            var dexInfo = this.ROM.GetDexEntry(id);
            SpeciesTextBox.Text = dexInfo.Species.ToString();
            HeightTextBox.Text = String.Format("{0}' {1}''", dexInfo.HeightFeet, dexInfo.HeightInches);
            WeightTextBox.Text = String.Format("{0} lb", dexInfo.Weight.ToString("N1"));
            DescriptionTextBox.Text = dexInfo.Text;

            //Starting moves
            StartMove1TextBox.Text = baseStats.Move1 == 0 ? "" : this.ROM.GetMoveName(baseStats.Move1);
            StartMove2TextBox.Text = baseStats.Move2 == 0 ? "" : this.ROM.GetMoveName(baseStats.Move2);
            StartMove3TextBox.Text = baseStats.Move3 == 0 ? "" : this.ROM.GetMoveName(baseStats.Move3);
            StartMove4TextBox.Text = baseStats.Move4 == 0 ? "" : this.ROM.GetMoveName(baseStats.Move4);

            //Learnable TMs/HMs
            TMHMDataGridView.Rows.Clear();
            for (int i = 0; i < baseStats.TMHM.Length - 1; i++)
            {
                if (!baseStats.TMHM[i]) continue;

                var toAdd = new String[2];
                if(i < 50)
                    toAdd[0] = "TM" + (i + 1).ToString().PadLeft(2, '0');
                else
                    toAdd[0] = "HM" + (i + 1 - 50).ToString().PadLeft(2, '0');
                toAdd[1] = this.ROM.GetTMHMMoveName((byte)(i + 1));

                TMHMDataGridView.Rows.Add(toAdd);
            }

            //Get EvosMoves object
            var evosMoves = this.ROM.GetMonEvosMoves(id);

            //Evolutions
            EvolutionsDataGridView.Rows.Clear();

            String[] tmpEvo;
            foreach(var evos in evosMoves.Evolutions)
            {
                tmpEvo = new String[4];

                tmpEvo[0] = evos.Method.ToString();
                tmpEvo[1] = evos.MinLevel.ToString();
                tmpEvo[2] = evos.Item == null ? "" : this.ROM.GetItemName(evos.Item.Value);
                tmpEvo[3] = this.ROM.GetMonName(evos.EvolveTo);

                EvolutionsDataGridView.Rows.Add(tmpEvo);
            }

            //Learnset
            MovesByLevelDataGridView.Rows.Clear();

            String[] tmpMove;
            foreach(var move in evosMoves.Learnset)
            {
                tmpMove = new string[2];

                tmpMove[0] = move.Level.ToString();
                tmpMove[1] = this.ROM.GetMoveName(move.Move);

                MovesByLevelDataGridView.Rows.Add(tmpMove);
            }

            //Growth Formula
            var growthFormula = this.ROM.GetGrowthFormula(baseStats.GrowthTable);

            var painter = new MathPainter { LaTeX = growthFormula };
            var png = painter.DrawAsStream();
            var pngImg = Image.FromStream(png);

            GrowthFormulaPictureBox.Image = pngImg;

            //Enable export buttons
            ExportFrontSpriteButton.Enabled = true;
            ExportBackSpriteButton.Enabled = true;
        }

        private void FrontSpriteColorCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var obj = (CheckBox)sender;

            var comboboxItem = (ComboboxItem)MonListComboBox.SelectedItem;

            byte id = (byte)comboboxItem.Value;
            byte dex = this.ROM.GetDexFromId(id);
            var baseStats = this.ROM.GetMonBaseStats(dex);
            var spritePalette = this.ROM.GetMonPalette(dex);

            var decompressor = new SpriteDecompressor(this.ROM);

            decompressor.DecompressFront(id);
            var frontSprite = decompressor.RenderSprite(obj.CheckState == CheckState.Checked ? spritePalette : null);
            this.FrontSpritePictureBox.Image = frontSprite;
        }

        private void BackSpriteColorCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var obj = (CheckBox)sender;

            var comboboxItem = (ComboboxItem)MonListComboBox.SelectedItem;

            byte id = (byte)comboboxItem.Value;
            byte dex = this.ROM.GetDexFromId(id);
            var baseStats = this.ROM.GetMonBaseStats(dex);
            var spritePalette = this.ROM.GetMonPalette(dex);

            var decompressor = new SpriteDecompressor(this.ROM);

            decompressor.DecompressBack(id);
            var backSprite = decompressor.RenderSprite(obj.CheckState == CheckState.Checked ? spritePalette : null);
            this.BackSpritePictureBox.Image = backSprite;
        }

        private void ExportFrontSpriteButton_Click(object sender, EventArgs e)
        {
            var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitamp Image|*.bmp|GIF Image|*.gif";
            saveDialog.Title = "Export front sprite";
            saveDialog.ShowDialog();

            if (saveDialog.FileName == "") return;

            FileStream fs = (FileStream)saveDialog.OpenFile();
            
            switch(saveDialog.FilterIndex)
            {
                case 1:
                    FrontSpritePictureBox.Image.Save(fs, ImageFormat.Png);
                    break;
                case 2:
                    FrontSpritePictureBox.Image.Save(fs, ImageFormat.Jpeg);
                    break;
                case 3:
                    FrontSpritePictureBox.Image.Save(fs, ImageFormat.Bmp);
                    break;
                case 4:
                    FrontSpritePictureBox.Image.Save(fs, ImageFormat.Gif);
                    break;
                default:
                    FrontSpritePictureBox.Image.Save(fs, ImageFormat.Png);
                    break;
            }

            fs.Close();
        }

        private void ExportBackSpriteButton_Click(object sender, EventArgs e)
        {
            var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitamp Image|*.bmp|GIF Image|*.gif";
            saveDialog.Title = "Export back sprite";
            saveDialog.ShowDialog();

            if (saveDialog.FileName == "") return;

            FileStream fs = (FileStream)saveDialog.OpenFile();

            switch (saveDialog.FilterIndex)
            {
                case 1:
                    BackSpritePictureBox.Image.Save(fs, ImageFormat.Png);
                    break;
                case 2:
                    BackSpritePictureBox.Image.Save(fs, ImageFormat.Jpeg);
                    break;
                case 3:
                    BackSpritePictureBox.Image.Save(fs, ImageFormat.Bmp);
                    break;
                case 4:
                    BackSpritePictureBox.Image.Save(fs, ImageFormat.Gif);
                    break;
                default:
                    BackSpritePictureBox.Image.Save(fs, ImageFormat.Png);
                    break;
            }

            fs.Close();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void LoadedRom_Load(object sender, EventArgs e)
        {

        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }
    }
}
