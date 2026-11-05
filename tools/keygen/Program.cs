using System;
using System.Security.Cryptography;
using System.Text;

// Generates the stable Chrome extension identity used by the unpacked build.
// Chrome derives the extension ID from the SHA-256 of the DER public key and
// maps each nibble 0-f onto a-p.
using var rsa = RSA.Create(2048);
var spki = rsa.ExportSubjectPublicKeyInfo();
var key = Convert.ToBase64String(spki);
var hash = SHA256.HashData(spki);
var sb = new StringBuilder(32);
for (int i = 0; i < 16; i++)
{
    sb.Append((char)('a' + (hash[i] >> 4)));
    sb.Append((char)('a' + (hash[i] & 0xF)));
}

Console.WriteLine(key);
Console.WriteLine(sb.ToString());
