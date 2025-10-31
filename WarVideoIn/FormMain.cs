using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WarVideoIn;

public partial class FormMain : Form
{
    // ������ ������
    // 0x70, 0x70 - ZVO ��������� (2 �����)
    // 0x01 - ��� ������ (1 ����) 1 - ���� �����
    // 0x00000000 - ����� ����� (4 �����)
    // 0x0000 - ������� ����� ����� ������ (2 �����)
    // 0x0000 - ����� ������ ������ (2 �����)
    // 0x0000 - ������� ������ �������� �������� (2 �����)
    // [N].. ���� �������� ��������
    private const int LenHeader = 2 + 1 + 4 + 2 + 2 + 2;

    private readonly OpenCvSharp.Size resolution = new(320, 240);
    private UdpClient udp = new();

    public FormMain()
    {
        InitializeComponent();

        Shown += FormMain_Shown;
    }

    private void FormMain_Shown(object? sender, EventArgs e)
    {
        udp.Client.ReceiveBufferSize = 65536 * 1000;
        udp.Client.SendBufferSize = 65536 * 1000;
        udp.EnableBroadcast = true;
        udp.MulticastLoopback = true;
        //udp.Client.Bind(new IPEndPoint(IPAddress.Parse("192.168.0.254"), 7777)); // �������
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 7777)); // wifi

        ReceiveAsync();
    }

    private async void ReceiveAsync(CancellationToken ct = default)
    {
        // ������ ������
        // 0x70, 0x70 - ZVO ��������� (2 �����)
        // 0x01 - ��� ������ (1 ����) 1 - ���� �����
        // 0x00000000 - ����� ����� (4 �����)
        // 0x0000 - ������� ����� ����� ������ (2 �����)
        // 0x0000 - ����� ������ ������ (2 �����)
        // 0x0000 - ������� ������ �������� �������� (2 �����)
        // [N].. ���� �������� ��������
        byte[] rgb = new byte[resolution.Width * resolution.Height * 3];
        int lastFrame = 0; // ������� ����
        while (!ct.IsCancellationRequested)
        {
            var data = await udp.ReceiveAsync();

            var pack = data.Buffer;
            if (pack.Length <= LenHeader) continue; // ������� ��������� ������ ������
            if (pack[0] != 0x70) continue; // �� ��������� HI
            if (pack[1] != 0x70) continue; // �� ��������� LO
            if (pack[2] != 0x01) continue; // �� ����� �����
            var frameNumber = BitConverter.ToInt32(pack, 3);
            var curr = BitConverter.ToUInt16(pack, 7);
            var all = BitConverter.ToUInt16(pack, 9);
            var cutLen = BitConverter.ToUInt16(pack, 11);
            if (pack.Length != LenHeader + cutLen) continue; // ������ ������ �� ���������!
            if (frameNumber != lastFrame) // ����� ����� ����, ���� ��������� �����������
            {
                lastFrame = frameNumber;
                using var mat = new Mat(resolution.Height, resolution.Width, MatType.CV_8UC3);
                Marshal.Copy(rgb, 0, mat.Data, rgb.Length);
                // ���������� ����
                Bitmap bitmap = BitmapConverter.ToBitmap(mat);
                if (pictureBoxMain.Image != null)
                {
                    pictureBoxMain.Image.Dispose();
                    pictureBoxMain.Image = null;
                }
                pictureBoxMain.Image = bitmap;

                rgb = new byte[rgb.Length];
            }
            Array.Copy(pack, 13, rgb, (curr - 1) * cutLen, cutLen);
        }
    }
}
