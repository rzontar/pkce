﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Owin.Infrastructure;

namespace Microsoft.Owin.Security.Jwt
{
    /// <summary>
    /// Implements a provider for self signed JWT, where an application 
    /// issues its own JWT for self consumption.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "This type is only controlled through the interface, which is not disposable.")]
    public class SelfSigningJwtProvider : ISigningCredentialsProvider
    {
        private readonly TimeSpan _keyExpiry;
        private readonly List<string> _audiences = new List<string>();
        private readonly Dictionary<Guid, SymmetricKeys> _signingKeys = new Dictionary<Guid, SymmetricKeys>();
        private readonly ReaderWriterLockSlim _syncLock = new ReaderWriterLockSlim();

        private Guid _currentKeyId = Guid.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfSigningJwtProvider"/> class.
        /// </summary>
        /// <param name="issuer">The issuer for the JWT.</param>
        public SelfSigningJwtProvider(string issuer)
        {
            if (string.IsNullOrWhiteSpace(issuer))
            {
                throw new ArgumentNullException("issuer");
            }

            Issuer = issuer;

            _keyExpiry = new TimeSpan(4, 0, 0);

            _audiences.Add(issuer);

            SystemClock = new SystemClock();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfSigningJwtProvider"/> class.
        /// </summary>
        /// <param name="issuer">The issuer for the JWT.</param>
        /// <param name="rotateCredentialsAfter">The time span a signing key is valid for.</param>
        public SelfSigningJwtProvider(string issuer, TimeSpan rotateCredentialsAfter) : this(issuer)
        {
            _keyExpiry = rotateCredentialsAfter;
        }

        /// <summary>
        /// Gets the JWT issuer.
        /// </summary>
        /// <value>
        /// The JWT issuer.
        /// </value>
        public string Issuer { get; private set; }

        /// <summary>
        /// The SystemClock provides access to the system's current time coordinates. If it is not provided a default instance is
        /// used which calls DateTimeOffset.UtcNow. This is typically not replaced except for unit testing. 
        /// </summary>
        public ISystemClock SystemClock { get; set; }

        /// <summary>
        /// Gets the credentials used to sign the JWT.
        /// </summary>
        /// <value>
        /// The credentials used to sign the JWT.
        /// </value>
        public SigningCredentials SigningCredentials
        {
            get
            {
                if (_currentKeyId == Guid.Empty ||
                    _signingKeys[_currentKeyId].ExpiresOn < SystemClock.UtcNow)
                {
                    RotateKey();
                }

                _syncLock.EnterReadLock();
                try
                {
                    return _signingKeys[_currentKeyId].SigningCredentials;
                }
                finally
                {
                    _syncLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets all known security tokens.
        /// </summary>
        /// <returns>
        /// All known security tokens.
        /// </returns>
        public IEnumerable<SecurityToken> SecurityTokens
        {
            get { return _signingKeys.Select(signingKey => signingKey.Value.SecurityToken).Cast<SecurityToken>().ToList(); }
        }

        private void RotateKey()
        {
            Guid keyIdentifer = Guid.NewGuid();
            using (var signingAlgorithm = new AesManaged())
            {
                var signingCredentials = new SymmetricKeys(signingAlgorithm.Key, keyIdentifer, _keyExpiry, SystemClock);

                _syncLock.EnterWriteLock();
                try
                {
                    _signingKeys.Add(keyIdentifer, signingCredentials);
                    _currentKeyId = keyIdentifer;

                    if (_signingKeys.Count <= 5)
                    {
                        return;
                    }

                    Guid oldestKeyIdentifier = _signingKeys.OrderBy(k => k.Value.ExpiresOn).First().Key;
                    _signingKeys.Remove(oldestKeyIdentifier);
                }
                finally
                {
                    _syncLock.ExitWriteLock();
                }
            }
        }

        private class SymmetricKeys
        {
            public SymmetricKeys(byte[] key, Guid keyIdentifier, TimeSpan expiresAfter, ISystemClock clock)
            {
                ExpiresOn = clock.UtcNow.Add(expiresAfter);

                string keyIdentifierString = keyIdentifier.ToString("D");

                var keyIdentifierClause = new NamedKeySecurityKeyIdentifierClause("kid", keyIdentifierString);
                var securityKeyIdentifer = new SecurityKeyIdentifier(new SecurityKeyIdentifierClause[] { keyIdentifierClause });
                SigningCredentials = new SigningCredentials(new InMemorySymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature, SecurityAlgorithms.Sha256Digest, securityKeyIdentifer);

                SecurityToken = new NamedKeySecurityToken(keyIdentifierString, new[] { new InMemorySymmetricSecurityKey(key) });
            }

            public DateTimeOffset ExpiresOn { get; private set; }

            public NamedKeySecurityToken SecurityToken { get; private set; }

            public SigningCredentials SigningCredentials { get; private set; }
        }
    }
}