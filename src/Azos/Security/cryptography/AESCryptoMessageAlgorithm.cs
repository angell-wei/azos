﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

using Azos.Conf;

namespace Azos.Security
{
  /// <summary>
  /// Implements AES256 CBC mode cipher with IV(crng-nonce/HMAC) authentication protection algorithm.
  /// The algorithm ensures that the data is encrypted AND can be decrypted back using the same HMAC, hence
  /// the algorithm uses 2 private keys: one for AES256 cipher, and one for HMAC authenticity check on decipher.
  /// </summary>
  /// <remarks>
  /// A message to be Protect()-ed is supplied as a byte[].
  /// First, we generate a crypto random nonce of byte[8], then
  /// we computer HMAC(nonce + originalMsg, pvt_key1).
  /// Notice, that the HMAC is based on the originalMessage AND the nonce which is random with every call (hence the name).
  /// An attacker may not "recreate" the original payload from hash (ensured by one-way HMAC).
  /// The protected message is thus:
  /// <code>
  ///  [nonce 8 byte][HMAC(pvt,nonce,msg) 16 byte][AES256 CBC having IV = HMAC]
  /// </code>
  /// The same unprotected payload yields different protected content due to the usage of nonce, avalanche effect of HMAC, and CBC mode of AES using IV.
  ///
  /// The Unprotect() phase repeats the process in reverse, applying AES256 decipher using IV from HMAC,
  /// then re-computes the HMAC to ensure that deciphered byte[] re-hashes into the same HMAC for its private key.
  /// </remarks>
  public sealed class AESCryptoMessageAlgorithm : CryptoMessageAlgorithm
  {
    //private const int NONCE_LEN = 8;
    //private const int MD5_HASH_LEN = 128 / 8;
    //private const int HEADER_LEN = NONCE_LEN + MD5_HASH_LEN;


    private const int IV_LEN = 128 / 8;
    private const int HMAC_LEN = 256 / 8;
    private const int HDR_LEN = IV_LEN + HMAC_LEN;

    public AESCryptoMessageAlgorithm(ICryptoManagerImplementation director, IConfigSectionNode config) : base(director, config)
    {
    }

    [Config]
    private byte[] m_HMACKey;

    [Config]
    private byte[] m_AESKey;


    public override CryptoMessageAlgorithmFlags Flags => CryptoMessageAlgorithmFlags.Cipher | CryptoMessageAlgorithmFlags.CanUnprotect;


    public override byte[] Protect(ArraySegment<byte> originalMessage)
    {
      originalMessage.Array.NonNull(nameof(originalMessage));

      if (originalMessage.Count < 1)
        throw new SecurityException(StringConsts.ARGUMENT_ERROR + "{0}.Protect(originalMessage.len < 1)".Args(GetType().Name));

      var iv = ComponentDirector.GenerateRandomBytes(IV_LEN);

      using (var aes = new AesManaged())
      {
        aes.Mode = CipherMode.CBC;
        aes.KeySize = 256;
        aes.Padding = PaddingMode.PKCS7;
        using (var encryptor = aes.CreateEncryptor(m_AESKey, iv))
        {
          var encrypted = encryptor.TransformFinalBlock(originalMessage.Array, originalMessage.Offset, originalMessage.Count);

          var hmac = getHMAC(new ArraySegment<byte>(iv), originalMessage);
          var header = iv.AppendToNew(hmac);
          return header.AppendToNew(encrypted);
        }
      }

    }

    public override byte[] Unprotect(ArraySegment<byte> protectedMessage)
    {
      protectedMessage.Array.NonNull(nameof(protectedMessage));
      if (protectedMessage.Count < HDR_LEN + 1)
        throw new SecurityException(StringConsts.ARGUMENT_ERROR + "{0}.Unprotect(protectedMessage.Count < {1})".Args(GetType().Name, HDR_LEN));

      var iv = new byte[IV_LEN];
      var hmac = new byte[HMAC_LEN];
      Array.Copy(protectedMessage.Array, protectedMessage.Offset, iv, 0, IV_LEN);
      Array.Copy(protectedMessage.Array, protectedMessage.Offset + IV_LEN, hmac, 0, HMAC_LEN);

      using (var aes = new AesManaged())
      {
        aes.Mode = CipherMode.CBC;
        aes.KeySize = 256;
        aes.Padding = PaddingMode.PKCS7;
        using (var decrypt = aes.CreateDecryptor(m_AESKey, hmac))
        {
          var decrypted = decrypt.TransformFinalBlock(protectedMessage.Array, protectedMessage.Offset + HDR_LEN, protectedMessage.Count - HDR_LEN);

          //rehash locally and check
          var rehmac = getHMAC(new ArraySegment<byte>(iv), new ArraySegment<byte>(decrypted));
          if (!hmac.MemBufferEquals(rehmac)) return null;//HMAC mismatch: message has been tampered with

          return decrypted;
        }
      }
    }


    //////public override byte[] XXXProtect(ArraySegment<byte> originalMessage)
    //////{
    //////  originalMessage.Array.NonNull(nameof(originalMessage));

    //////  if (originalMessage.Count < 1)
    //////    throw new SecurityException(StringConsts.ARGUMENT_ERROR + "{0}.Protect(originalMessage.len < 1)".Args(GetType().Name));

    //////  var nonce = Platform.RandomGenerator.Instance.NextRandomBytes(NONCE_LEN);
    //////  var hmac = getHMAC(new ArraySegment<byte>(nonce), originalMessage);

    //////  using(var aes = new AesManaged())
    //////  {
    //////    aes.Mode = CipherMode.CBC;
    //////    aes.KeySize = 256;
    //////    aes.Padding = PaddingMode.None;
    //////    using(var encryptor = aes.CreateEncryptor(m_AESKey, hmac))
    //////    {
    //////      var encrypted = encryptor.TransformFinalBlock(originalMessage.Array, originalMessage.Offset, originalMessage.Count);

    //////      var header = nonce.AppendToNew(hmac);
    //////      return header.AppendToNew(encrypted);
    //////    }
    //////  }
    //////}

    //////public override byte[] XXXUnprotect(ArraySegment<byte> protectedMessage)
    //////{
    //////  protectedMessage.Array.NonNull(nameof(protectedMessage));
    //////  if (protectedMessage.Count < HEADER_LEN + 1)
    //////    throw new SecurityException(StringConsts.ARGUMENT_ERROR + "{0}.Unprotect(protectedMessage.Count < {1})".Args(GetType().Name, HEADER_LEN));

    //////  var hmac = new byte[MD5_HASH_LEN];
    //////  Array.Copy(protectedMessage.Array, protectedMessage.Offset, hmac, NONCE_LEN, MD5_HASH_LEN);
    //////  using (var aes = new AesManaged())
    //////  {
    //////    aes.Mode = CipherMode.CBC;
    //////    aes.KeySize = 256;
    //////    aes.Padding = PaddingMode.None;
    //////    using (var decrypt = aes.CreateDecryptor(m_AESKey, hmac))
    //////    {
    //////      var decrypted = decrypt.TransformFinalBlock(protectedMessage.Array, protectedMessage.Offset+HEADER_LEN, protectedMessage.Count-HEADER_LEN);

    //////      //rehash locally and check
    //////      var rehmac = getHMAC(new ArraySegment<byte>(protectedMessage.Array, 0, NONCE_LEN), new ArraySegment<byte>(decrypted));
    //////      if (!hmac.MemBufferEquals(rehmac)) return null;//HMAC mismatch: message has been tampered with

    //////      return decrypted;
    //////    }
    //////  }
    //////}

    private byte[] getHMAC(ArraySegment<byte> nonce, ArraySegment<byte> data)
    {
      using(var ihash = IncrementalHash.CreateHMAC(HashAlgorithmName.MD5, m_HMACKey))
      {
       ihash.AppendData(nonce.Array, nonce.Offset, nonce.Count);
       ihash.AppendData(data.Array, data.Offset, data.Count);
       return ihash.GetHashAndReset();
      }
    }

  }
}