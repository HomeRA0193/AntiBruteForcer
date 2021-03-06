﻿Imports System.IO
Imports System.Text
Imports System.Security.Cryptography
Imports Jebtek.RdRand
Imports Scrypt

Public Class MainForm
    Private Const _saltBlockSize As Integer = 20 '20
    Private Const _nIters As Integer = 262144 '262144
    Private Const _nItersFast As Integer = _nIters / 8 '_nIters / 8
    Private Const _deriveBlocksCount As Integer = 10 '10
    Private Const _messageWidth = 16 '16
    Private Const _rndPassLen = 8 '8
    Private Const _encryptedSaltSize = 479 '479
    Private Const _fileSaltCopies = 2184 '2184
    Private Const _encryptedSaltMessage = "Encrypted 'salt message'..."
    Private Const _saltTitle As String = "AntiBruteForcer ENCRYPTED SALT"
    Private Const _keyTitle As String = "AntiBruteForcer KEY"

    Private _rdRand As Boolean = False
    Private _distrMap As Long()
    Private _salt As New List(Of Byte)

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = My.Application.Info.Title.ToString() + " [" + My.Application.Info.Version.ToString() + "]"
        RDRandomGeneratorAvailableCheck()
    End Sub

    Private Sub RDRandomGeneratorAvailableCheck()
        Dim rdRandomGeneratorAvailable = False
        Try
            rdRandomGeneratorAvailable = RdRandom.GeneratorAvailable()
        Catch
        End Try
        If rdRandomGeneratorAvailable Then
            _IntelInsidePictureBox.Visible = True : _rdRand = True
        Else
            _IntelInsidePictureBox.Visible = False : _rdRand = False
        End If
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        _encryptedSaltRichTextBox.ClearUndo() : _encryptedSaltRichTextBox.Clear()
        _saltPasswordTextBox.ClearUndo() : _saltPasswordTextBox.Clear()
        _keyRichTextBox.ClearUndo() : _keyRichTextBox.Clear()
        _masterPasswordTextBox.ClearUndo() : _masterPasswordTextBox.Clear()

        Clipboard.Clear()
    End Sub

    Private Sub UpdateEncryptedSalt(e As MouseEventArgs)
        If _encryptedSaltRichTextBox.Text.Trim() <> _encryptedSaltMessage Then
            _encryptedSaltRichTextBox.Text = GetEncryptedSalt(e, _saltPasswordTextBox.Text)
            Dim encryptedSaltBytes = GetEncryptedSaltBytes()
            If _encryptedSaltSize <> encryptedSaltBytes.Length Then
                Throw New Exception("_encryptedSaltSize <> encryptedSaltBytes.Length")
            End If
        End If
    End Sub

    Private Function GetEncryptedSaltBytes() As Byte()
        Using ms = New MemoryStream
            Using sw = New StreamWriter(ms, Encoding.ASCII)
                sw.Write(_encryptedSaltRichTextBox.Text) : sw.Flush()
                ms.Seek(0, SeekOrigin.Begin)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Sub SetEncryptedSaltFromStream(stream As Stream, bytesFromEnd As Long)
        bytesFromEnd = If((stream.Length - bytesFromEnd) < 0, stream.Length, bytesFromEnd)
        stream.Seek(-bytesFromEnd, SeekOrigin.End)
        Dim sizeToRead = stream.Length - stream.Position
        Dim rawBuffer = New Byte(sizeToRead - 1) {}
        Dim dataCounter = stream.Read(rawBuffer, 0, sizeToRead)
        If dataCounter <> sizeToRead Then
            Throw New Exception("dataCounter <> sizeToRead")
        End If
        Dim decoded = Base64Sync.Decode(rawBuffer, _saltTitle, _messageWidth)
        Dim encoded = Base64Sync.Encode(decoded, _saltTitle, _messageWidth)
        Using ms = New MemoryStream(encoded)
            Using sr = New StreamReader(ms, Encoding.ASCII)
                Dim encryptedSalt = sr.ReadToEnd()
                If _encryptedSaltSize <> encryptedSalt.Length Then
                    Throw New Exception("_encryptedSaltSize <> encryptedSalt.Length")
                End If
                With _encryptedSaltRichTextBox
                    .ClearUndo()
                    .Clear()
                    .Text = encryptedSalt.Trim()
                End With
            End Using
        End Using
    End Sub

    Private Function GetEncryptedSalt(e As MouseEventArgs, saltPassword As String) As String
        _salt.AddRange(Encoding.UTF8.GetBytes(e.X.ToString()))
        _salt.AddRange(Encoding.UTF8.GetBytes(e.Y.ToString()))
        _salt.AddRange(Encoding.UTF8.GetBytes(Date.Now.Ticks.ToString()))
        _salt.AddRange(Guid.NewGuid().ToByteArray())
        If _rdRand Then _salt.AddRange(RdRandom.GenerateBytes(256 / 8))
        Dim saltHash = Hash1600(_salt.ToArray(), 32)
        _salt.Clear() : _salt.AddRange(saltHash)

        Dim salt1 = _salt.ToArray()
        Dim saltEnc1 = RijndaelEncryptor.Encode(salt1, Encoding.UTF8.GetBytes(saltPassword))
        Dim saltBase64Enc = Base64Sync.Encode(saltEnc1, _saltTitle, _messageWidth)
        Dim saltEnc2 = Base64Sync.Decode(saltBase64Enc, _saltTitle, _messageWidth)
        For i = 0 To saltEnc1.Length - 1
            If saltEnc1(i) <> saltEnc2(i) Then
                Throw New Exception("saltEnc1 <> saltEnc2")
                Exit For
            End If
        Next
        Dim salt2 = RijndaelEncryptor.Decode(saltEnc2, Encoding.UTF8.GetBytes(saltPassword))
        For i = 0 To salt1.Length - 1
            If salt1(i) <> salt2(i) Then
                Throw New Exception("salt1 <> salt2")
                Exit For
            End If
        Next

        Return Encoding.UTF8.GetString(saltBase64Enc)
    End Function

    Private Sub _deriveKeyButton_Click(sender As Object, e As EventArgs) Handles _deriveKeyButton.Click
        Try
            _saltPasswordTextBox.Visible = False : _saltPasswordRndButton.Visible = False : _saltPasswordTextBoxLabel.Visible = False
            _saltDecryptionCheckBox.Visible = False : _masterPasswordTextBox.Visible = False : _masterPasswordRndButton.Visible = False
            _masterPasswordTextBoxLabel.Visible = False : _embedReadButton.Visible = False : _96bitKeyCheckBox.Visible = False
            _384bitKeyCheckBox.Visible = False : _saltGenerationCheckBox.Visible = False : _helpButton.Visible = False
            _fastCheckBox.Visible = False : _deriveKeyButton.Visible = False : _encryptedSaltCopyButton.Visible = False
            _keyCopyButton.Visible = False

            _saltGenerationCheckBox.Checked = False : _encryptedSaltRichTextBox.Focus()

            _keyRichTextBox.Text = "Started to derive key..." + vbCrLf
            If Not _saltDecryptionCheckBox.Checked Then UpdateEncryptedSalt(e)
            _saltPasswordTextBox.Text = _saltPasswordTextBox.Text.Trim()
            _masterPasswordTextBox.Text = _masterPasswordTextBox.Text.Trim()
            Application.DoEvents()
            Dim password = Encoding.UTF8.GetBytes(_masterPasswordTextBox.Text)
            Dim salt = New List(Of Byte)(RijndaelEncryptor.Decode(Base64Sync.Decode(Encoding.UTF8.GetBytes(_encryptedSaltRichTextBox.Text), _saltTitle, _messageWidth),
                                                                  Encoding.UTF8.GetBytes(_saltPasswordTextBox.Text)))
            Dim resultKey = New List(Of Byte)
            For i = 0 To _deriveBlocksCount - 1
                Dim saltBlock = New Byte(_saltBlockSize - 1) {} : salt.CopyTo((_saltBlockSize * i), saltBlock, 0, _saltBlockSize)
                Dim keyBlockS = ScryptEncoder.CryptoScrypt(password, saltBlock, If(_fastCheckBox.Checked, _nItersFast, _nIters), 16, 1) '256 bit, 512 Mb RAM!
                Dim keyBlockR = New Rfc2898DeriveBytes(password, saltBlock, If(_fastCheckBox.Checked, _nItersFast, _nIters)).GetBytes(_saltBlockSize) '160 bit
                Dim hmacA = New HMACSHA1(keyBlockS) : Dim keyBlockA = hmacA.ComputeHash(keyBlockR)
                Dim hmacB = New HMACSHA256(keyBlockR) : Dim keyBlockB = hmacB.ComputeHash(keyBlockS)
                resultKey.AddRange(keyBlockA)
                Dim saltEnc = New MemoryStream(RijndaelEncryptor.Encode(salt.ToArray(), keyBlockB, False))
                salt = New List(Of Byte)(saltEnc.ToArray())
                _keyRichTextBox.AppendText(((_deriveBlocksCount - 1) - i).ToString()) : Application.DoEvents()
            Next
            Dim result As Byte() = Nothing
            If _96bitKeyCheckBox.Checked Or _384bitKeyCheckBox.Checked Then
                Dim H2 As New SHA384Managed
                Dim shortKeyStream = New MemoryStream(H2.ComputeHash(resultKey.ToArray()))
                shortKeyStream.Seek(0, SeekOrigin.Begin)
                If _96bitKeyCheckBox.Checked Then shortKeyStream.SetLength(12) Else shortKeyStream.SetLength(48)
                result = shortKeyStream.ToArray()
            Else
                result = resultKey.ToArray()
            End If
            With _keyRichTextBox
                .ClearUndo()
                .Clear()
                .Text = Encoding.UTF8.GetString(Base64Sync.Encode(result, _keyTitle, _messageWidth))
            End With
        Catch
            MessageBox.Show("Can't derive key!")
            _keyRichTextBox.ClearUndo() : _keyRichTextBox.Clear()
        Finally
            _saltPasswordTextBox.Visible = True : _saltPasswordRndButton.Visible = True : _saltPasswordTextBoxLabel.Visible = True
            _saltDecryptionCheckBox.Visible = True : _masterPasswordTextBox.Visible = True : _masterPasswordRndButton.Visible = True
            _masterPasswordTextBoxLabel.Visible = True : _embedReadButton.Visible = True : _96bitKeyCheckBox.Visible = True
            _384bitKeyCheckBox.Visible = True : _saltGenerationCheckBox.Visible = True : _helpButton.Visible = True
            _fastCheckBox.Visible = True : _deriveKeyButton.Visible = True : _encryptedSaltCopyButton.Visible = True
            _keyCopyButton.Visible = True
        End Try
    End Sub

    Private Sub _encryptedSaltCopyButton_Click(sender As Object, e As EventArgs) Handles _encryptedSaltCopyButton.Click
        Try
            Dim saltSb = New StringBuilder()
            For Each s In _encryptedSaltRichTextBox.Lines
                saltSb.Append(s) : saltSb.Append(ControlChars.CrLf)
            Next
            Clipboard.SetText(saltSb.ToString())
            _encryptedSaltRichTextBox.ClearUndo() : _encryptedSaltRichTextBox.Clear()
        Catch
            MessageBox.Show("Can't copy encrypted 'salt' to clipboard!")
        End Try
    End Sub

    Private Sub _keyCopyButton_Click(sender As Object, e As EventArgs) Handles _keyCopyButton.Click
        Try
            Dim keySb = New StringBuilder()
            For i = 1 To _keyRichTextBox.Lines.Length - 2
                keySb.Append(_keyRichTextBox.Lines(i).Replace(vbCr, String.Empty))
            Next
            Clipboard.SetText(keySb.ToString())
            _keyRichTextBox.ClearUndo() : _keyRichTextBox.Clear()
        Catch
            MessageBox.Show("Can't copy derived key to clipboard!")
        End Try
    End Sub

    Private Sub _saltDecryptionCheckBox_CheckedChanged(sender As Object, e As EventArgs) Handles _saltDecryptionCheckBox.CheckedChanged
        If _saltDecryptionCheckBox.Checked Then
            With _saltGenerationCheckBox
                .Checked = False
                .Enabled = False
            End With
            _IntelInsidePictureBox.Visible = False
            _encryptedSaltRichTextBox.Clear()
            _embedReadButton.Text = "READ SALT (FILE)"
        Else
            _embedReadButton.Text = "EMBED SALT (FILE)"
            With _saltGenerationCheckBox
                .Enabled = True
            End With
            RDRandomGeneratorAvailableCheck()
        End If
    End Sub

    Private Sub UpdateEncryptedSalt(sender As Object, e As MouseEventArgs)
        If _saltGenerationCheckBox.Checked AndAlso Not _saltDecryptionCheckBox.Checked Then
            UpdateEncryptedSalt(e)
        End If
    End Sub

    Private Sub UpdateEncryptedSalt()
        Dim mea = New MouseEventArgs(MouseButtons.None, 0, DateTime.Now.Ticks Mod Integer.MaxValue, DateTime.Now.Ticks Mod If(_saltPasswordTextBox.Text.Length <> 0, _saltPasswordTextBox.Text.Length, 1), 0)
        If Not _saltDecryptionCheckBox.Checked Then
            UpdateEncryptedSalt(mea)
        End If
    End Sub

    Private Sub _saltGenerationCheckBox_CheckedChanged(sender As Object, e As EventArgs) Handles _saltGenerationCheckBox.CheckedChanged
        If _saltGenerationCheckBox.Checked Then
            With _encryptedSaltRichTextBox
                .ClearUndo()
                .Clear()
            End With

            With _keyRichTextBox
                .ClearUndo()
                .Clear()
            End With

            With _saltPasswordTextBox
                .ClearUndo()
                .Clear()
                .PasswordChar = "•"
            End With

            With _masterPasswordTextBox
                .ClearUndo()
                .Clear()
                .PasswordChar = "•"
            End With
        End If
    End Sub

    Private Sub _helpButton_Click(sender As Object, e As EventArgs) Handles _helpButton.Click
        _saltGenerationCheckBox.Checked = False
        With _encryptedSaltRichTextBox
            .ClearUndo()
            .Text = _encryptedSaltMessage
        End With

        With _saltPasswordTextBox
            .ClearUndo()
            .Clear()
            .Text = "Password to encrypt 'salt message' (private)..."
            .PasswordChar = String.Empty
        End With

        With _keyRichTextBox
            .ClearUndo()
            .Clear()
            .Text = "Derived key (to encrypt archive)..."
        End With

        With _masterPasswordTextBox
            .ClearUndo()
            .Clear()
            .Text = "MASTER PASSWORD (private)..."
            .PasswordChar = String.Empty
        End With
    End Sub

    Private Sub _saltPasswordRndButton_Click(sender As Object, e As EventArgs) Handles _saltPasswordRndButton.Click
        _saltPasswordTextBox.Text = String.Empty
        Dim es = GetEncryptedSalt(e, Guid.NewGuid().ToString("B"))
        Dim H4 As New SHA1Managed() '160 bit
        With _saltPasswordTextBox
            .PasswordChar = If(.PasswordChar = "•", String.Empty, "•")
            .Text = Convert.ToBase64String(H4.ComputeHash(Encoding.UTF8.GetBytes(es))).Substring(0, _rndPassLen)
        End With
        If _saltPasswordTextBox.PasswordChar = "•" Then
            _saltPasswordTextBox.Text = String.Empty
        End If
        If _saltPasswordTextBox.Text = String.Empty Then _saltPasswordRndButton_Click(sender, e)
        _deriveKeyButton.Focus()
    End Sub

    Private Sub _masterPasswordRndButton_Click(sender As Object, e As EventArgs) Handles _masterPasswordRndButton.Click
        _masterPasswordTextBox.Text = String.Empty
        Dim es = GetEncryptedSalt(e, Guid.NewGuid().ToString("B"))
        Dim H4 As New SHA1Managed() '160 bit
        With _masterPasswordTextBox
            .PasswordChar = If(.PasswordChar = "•", String.Empty, "•")
            .Text = Convert.ToBase64String(H4.ComputeHash(Encoding.UTF8.GetBytes(es))).Substring(0, _rndPassLen)
        End With
        If _masterPasswordTextBox.PasswordChar = "•" Then
            _masterPasswordTextBox.Text = String.Empty
        End If
        If _masterPasswordTextBox.Text = String.Empty Then _masterPasswordRndButton_Click(sender, e)
        _deriveKeyButton.Focus()
    End Sub

    Private Sub _embedReadButton_Click(sender As Object, e As EventArgs) Handles _embedReadButton.Click
        _saltGenerationCheckBox.Checked = False
        Try
            Dim ofd = New OpenFileDialog
            With ofd
                .RestoreDirectory = True
                .AddExtension = True
                .DefaultExt = ".*"
                .Filter = "All files (*.*)|*.*"
            End With
            If ofd.ShowDialog() = DialogResult.OK Then
                If _saltDecryptionCheckBox.Checked Then
                    If File.Exists(ofd.FileName) Then
                        Using fs = File.OpenRead(ofd.FileName)
                            SetEncryptedSaltFromStream(fs, _encryptedSaltSize)
                        End Using
                        MessageBox.Show("READ SALT (FILE): OK")
                    Else
                        MessageBox.Show("READ SALT (FILE): NO FILE!")
                    End If
                Else
                    If File.Exists(ofd.FileName) Then
                        File.SetAttributes(ofd.FileName, FileAttributes.Normal)
                        Dim encryptedSaltBytes As Byte()
                        Dim encryptedSaltBytesRead = New Byte(_encryptedSaltSize - 1) {}
                        Using fs = File.OpenWrite(ofd.FileName)
                            fs.Seek(0, SeekOrigin.End)
                            encryptedSaltBytes = GetEncryptedSaltBytes()
                            For i = 0 To _fileSaltCopies - 2
                                fs.Write(encryptedSaltBytes, 0, encryptedSaltBytes.Length)
                                fs.WriteByte(Encoding.ASCII.GetBytes(ControlChars.Lf)(0))
                            Next
                            fs.Write(encryptedSaltBytes, 0, encryptedSaltBytes.Length)
                            fs.Flush() : fs.Close()
                        End Using
                        Using fs = File.OpenRead(ofd.FileName)
                            Dim readCount = 0
                            With fs
                                .Seek(-_encryptedSaltSize, SeekOrigin.End)
                                readCount = .Read(encryptedSaltBytesRead, 0, _encryptedSaltSize)
                                .Close()
                            End With
                            If readCount <> _encryptedSaltSize Then
                                Throw New Exception("readCount <> _encryptedSaltSize")
                            End If
                            For i = 0 To _encryptedSaltSize - 1
                                If encryptedSaltBytes(i) <> encryptedSaltBytesRead(i) Then
                                    Throw New Exception("encryptedSaltBytes(i) <> encryptedSaltBytesRead(i)")
                                End If
                            Next
                        End Using
                        MessageBox.Show("EMBED SALT (FILE): OK (1 Mb, RECORDS: 2184)")
                    Else
                        MessageBox.Show("EMBED SALT (FILE): NO FILE!")
                    End If
                End If
            End If
        Catch ex As Exception
            MessageBox.Show("Can't process file!")
        End Try
    End Sub

    Private Sub _96bitKeyCheckBox_CheckedChanged(sender As Object, e As EventArgs) Handles _96bitKeyCheckBox.CheckedChanged
        If _96bitKeyCheckBox.Checked Then _384bitKeyCheckBox.Checked = False
    End Sub

    Private Sub _384bitKeyCheckBox_CheckedChanged(sender As Object, e As EventArgs) Handles _384bitKeyCheckBox.CheckedChanged
        If _384bitKeyCheckBox.Checked Then _96bitKeyCheckBox.Checked = False
    End Sub

    Private Sub MainForm_MouseMove(sender As Object, e As MouseEventArgs) Handles MyBase.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _encryptedSaltRichTextBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _encryptedSaltRichTextBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _saltPasswordTextBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _saltPasswordTextBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _saltPasswordRndButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _saltPasswordRndButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _saltPasswordTextBoxLabel_MouseMove(sender As Object, e As MouseEventArgs) Handles _saltPasswordTextBoxLabel.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _saltDecryptionCheckBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _saltDecryptionCheckBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _keyRichTextBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _keyRichTextBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _masterPasswordTextBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _masterPasswordTextBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _masterPasswordRndButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _masterPasswordRndButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _masterPasswordTextBoxLabel_MouseMove(sender As Object, e As MouseEventArgs) Handles _masterPasswordTextBoxLabel.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _embedReadButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _embedReadButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _96bitKeyCheckBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _96bitKeyCheckBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _384bitKeyCheckBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _384bitKeyCheckBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _saltGenerationCheckBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _saltGenerationCheckBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _helpButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _helpButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _IntelInsidePictureBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _IntelInsidePictureBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _fastCheckBox_MouseMove(sender As Object, e As MouseEventArgs) Handles _fastCheckBox.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _deriveKeyButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _deriveKeyButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _encryptedSaltCopyButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _encryptedSaltCopyButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _keyCopyButton_MouseMove(sender As Object, e As MouseEventArgs) Handles _keyCopyButton.MouseMove
        UpdateEncryptedSalt(sender, e)
    End Sub

    Private Sub _encryptedSaltRichTextBox_KeyPress(sender As Object, e As KeyPressEventArgs) Handles _encryptedSaltRichTextBox.KeyPress
        If Not _saltDecryptionCheckBox.Checked Then
            e.Handled = True
        End If
    End Sub

    Private Sub _keyRichTextBox_KeyPress(sender As Object, e As KeyPressEventArgs) Handles _keyRichTextBox.KeyPress
        e.Handled = True
    End Sub

    Private Function Hash1600(bytesToHash As Byte(), Optional paddingLength As Integer = 0) As Byte()
        Dim HR As New List(Of Byte)
        Dim H1 As New SHA512Managed    '512 bit
        Dim H2 As New SHA384Managed    '384 bit
        Dim H3 As New SHA256Managed    '256 bit
        Dim H4 As New SHA1Managed      '160 bit
        Dim H5 As New RIPEMD160Managed '160 bit
        Dim H6 As New MD5CryptoServiceProvider() '128 bit

        HR.AddRange(H1.ComputeHash(bytesToHash))
        HR.AddRange(H2.ComputeHash(bytesToHash))
        HR.AddRange(H3.ComputeHash(bytesToHash))
        HR.AddRange(H4.ComputeHash(bytesToHash))
        HR.AddRange(H5.ComputeHash(bytesToHash))
        HR.AddRange(H6.ComputeHash(bytesToHash))

        Dim padding = New Byte(paddingLength - 1) {}
        Dim rng As New RNGCryptoServiceProvider() : rng.GetBytes(padding)
        HR.AddRange(padding)

        Return HR.ToArray()
    End Function

    Private Sub _saltPasswordTextBox_TextChanged(sender As Object, e As EventArgs) Handles _saltPasswordTextBox.TextChanged
        If Not _saltDecryptionCheckBox.Checked Then
            UpdateEncryptedSalt()
        End If
    End Sub
End Class
