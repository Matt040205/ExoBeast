using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    public static class NetworkAddressHelper
    {
        public static string GetLocalIpAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkAddressHelper] Nao foi possivel obter IP local: {e.Message}");
            }

            return "127.0.0.1";
        }
    }
}
