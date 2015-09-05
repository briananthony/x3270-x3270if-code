﻿// Copyright (c) 2015 Paul Mattes.
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the names of Paul Mattes nor the names of his contributors
//       may be used to endorse or promote products derived from this software
//       without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY PAUL MATTES "AS IS" AND ANY EXPRESS OR IMPLIED
// WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
// EVENT SHALL PAUL MATTES BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
// OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
// ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using x3270if;

namespace UnitTests
{
    /// <summary>
    /// Tests for functionality internal to the session and feature methods, like string quoting.
    /// </summary>
    class InnerTests
    {
        public InnerTests()
        {
        }

        [TestFixtureSetUp]
        public void Setup()
        {
            Util.ConsoleDebug = false;
        }

        /// <summary>
        /// Exercise the JoinNonEmpty String extension.
        /// </summary>
        [Test]
        public void TestJoinNonEmpty()
        {
            string left;

            // If right is empty, return left.
            left = "bar";
            left += left.JoinNonEmpty(" ", null);
            Assert.AreEqual("bar", left);

            // If left is empty, return right.
            left = string.Empty;
            left += left.JoinNonEmpty(" ", "foo");
            Assert.AreEqual("foo", left);

            // Join them with a separator.
            left = "bar";
            left += left.JoinNonEmpty(" ", "foo");
            Assert.AreEqual("bar foo", left);
        }

        /// <summary>
        /// Test the QuoteString method.
        /// </summary>
        [Test]
        public void TestQuoteString()
        {
            // String that goes through unscathed.
            string s = Session.QuoteString("xxx", true);
            Assert.AreEqual("xxx", s);

            // Strings that just need double quotes.
            s = Session.QuoteString("hello there", true); // space
            Assert.AreEqual("\"hello there\"", s);
            s = Session.QuoteString("a,b", true); // comma
            Assert.AreEqual("\"a,b\"", s);
            s = Session.QuoteString("a(b", true); // left paren
            Assert.AreEqual("\"a(b\"", s);
            s = Session.QuoteString("a)b", true); // right paren
            Assert.AreEqual("\"a)b\"", s);

            // Strings that need backslashes.
            s = Session.QuoteString("a\"b", true); // double quote
            Assert.AreEqual("\"a\\\"b\"", s);
            s = Session.QuoteString(@"a\nb", true); // backslash
            Assert.AreEqual("\"a\\\\nb\"", s);

            // Backslashes that are left alone.
            s = Session.QuoteString(@"a\nb", false);
            Assert.AreEqual("\"a\\nb\"", s);

            // More than one of something, to make sure the whole string is scanned.
            s = Session.QuoteString("My, my (oh!) \"foo\"\\n", true);
            Assert.AreEqual("\"My, my (oh!) \\\"foo\\\"\\\\n\"", s);

            // Now the whole ASCII-7 character set, except the special characters, to make sure nothing else is molested.
            const string special = "\" ,()\\";
            string ascii7 = string.Empty;
            for (int i = 33; i < 127; i++)
            {
                if (!special.Contains((char)i))
                {
                    ascii7 += (char)i;
                }
            }
            s = Session.QuoteString(ascii7, true);
            Assert.AreEqual(ascii7, s);

            // Verify that known control characters are expanded and quotes are added.
            s = Session.QuoteString("hello\r\n\f\t\b", true);
            Assert.AreEqual("\"hello\\r\\n\\f\\t\\b\"", s);

            // Verify that other control characters are rejected.
            Assert.Throws<ArgumentException>(() => { s = Session.QuoteString("hello\x7fthere", true); });
        }

        /// <summary>
        /// Test the ExpandHostName method.
        /// </summary>
        [Test]
        public void TestExpandHostName()
        {
            // Start out with an x3270if session with no options, hence no default connect flags.
            var emulator = new ProcessSession();

            // Trivial version, does nothing.
            var s = emulator.ExpandHostName("host", null, null, ConnectFlags.None);
            Assert.AreEqual("host", s);

            // Make the host quotable.
            s = emulator.ExpandHostName("a:b::27", null, null, ConnectFlags.None);
            Assert.AreEqual("[a:b::27]", s);

            // Add a port.
            s = emulator.ExpandHostName("host", "port", null, ConnectFlags.None);
            Assert.AreEqual("host:port", s);

            // Add some LUs.
            s = emulator.ExpandHostName("host", null, new string[] { "lu1", "lu2" }, ConnectFlags.None);
            Assert.AreEqual("\"lu1,lu2@host\"", s);

            // Add some options.
            s = emulator.ExpandHostName("host", null, null, ConnectFlags.Secure);
            Assert.AreEqual("L:host", s);

            // Combine options, LUs and a port.
            // This is a little bit tricky to test, because the connect flags can appear in any order.
            // Otherwise, the order of the elements is fixed.
            s = emulator.ExpandHostName("1::2", "port", new string[] { "lu1", "lu2" }, ConnectFlags.Secure | ConnectFlags.NonTN3270E);
            Assert.IsTrue(s.StartsWith("\""));
            Assert.IsTrue(s.EndsWith("\""));
            s = s.Substring(1, s.Length - 2);
            const string luHostPort = "lu1,lu2@[1::2]:port";
            Assert.IsTrue(s.EndsWith(luHostPort));
            var flags = s.Substring(0, s.Length - luHostPort.Length);
            Assert.AreEqual(4, flags.Length);
            Assert.IsTrue(flags.Contains("L:"));
            Assert.IsTrue(flags.Contains("N:"));

            // Try all of the connect flags.
            s = emulator.ExpandHostName("host", null, null, ConnectFlags.All);
            const string luHostPort2 = "host";
            Assert.IsTrue(s.EndsWith(luHostPort2));
            flags = s.Substring(0, s.Length - luHostPort2.Length);
            Assert.AreEqual(12, flags.Length);
            Assert.IsTrue(flags.Contains("C:"));
            Assert.IsTrue(flags.Contains("L:"));
            Assert.IsTrue(flags.Contains("N:"));
            Assert.IsTrue(flags.Contains("P:"));
            Assert.IsTrue(flags.Contains("S:"));
            Assert.IsTrue(flags.Contains("B:"));

            // Try a session with non-default connect flags.
            var portEmulator = new PortSession(new PortConfig { AutoStart = false, DefaultConnectFlags = ConnectFlags.Secure });
            s = portEmulator.ExpandHostName("host", null, null, ConnectFlags.None);
            Assert.AreEqual("L:host", s);

            // Make sure the specific connect flags override the defaults (and are not ORed in).
            s = portEmulator.ExpandHostName("host", null, null, ConnectFlags.NonTN3270E);
            Assert.AreEqual("N:host", s);

            // Check the exceptions thrown on bad hostname, port, and LU.
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host/wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host@wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", "port/wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", "port:wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", "port.wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", "port@wrong"); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu/wrong" }); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu-okay", "lu:wrong" }); });
            Assert.Throws<ArgumentException>(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu@wrong" }); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("123:456::1.2.3.4"); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host-okay"); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host_okay"); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host", "port-okay"); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host", "port_okay"); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu.okay" }); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu-okay" }); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("host", lus: new string[] { "lu_okay" }); });
            Assert.DoesNotThrow(() => { var ch = portEmulator.ExpandHostName("года.ru"); });
            Assert.DoesNotThrow(() => { var ch = emulator.ExpandHostName("六.cn"); });
        }
    }
}