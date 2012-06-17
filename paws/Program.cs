using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenPop.Common.Logging;
using OpenPop.Mime;
using OpenPop.Mime.Decode;
using OpenPop.Mime.Header;
using OpenPop.Pop3;

namespace paws
{
    class Meeting
    {
        public String startDate;
        public String endDate;
        public String location;
        public String title;
        public Person organizer;
        public List<Person> attendees = new List<Person>();
    }

    class Person
    {
        public String name;
        public String email;
    }

    class MailboxScanner
    {
        private void ParseICalendar(byte[] attachment)
        {
            List<String> lines = new List<string>();

            // First tokenize each line.
            int p = 0;
            for (int i = 0; i < attachment.Length; i++)
            {
                if ((attachment[i] == '\n') && (i > 0) && (attachment[i - 1] == '\r')) {
                    String s = ASCIIEncoding.ASCII.GetString(attachment, p, i - 1 - p);
                    if ((attachment[p] == 0x20) || (attachment[p] == 0x09))
                    {
                        String s2 = lines.Last();
                        lines.RemoveAt(lines.Count - 1);
                        lines.Add(s2 + s);
                    }
                    else
                    {
                        lines.Add(s);
                    }

                    p = i + 1;
                }
            }

            // Then find the relevant lines.
            // Make sure this is a VCALENDAR attachment.
            if (lines[0].StartsWith("BEGIN:VCALENDAR"))
            {
                Meeting m = new Meeting();
                ExtractAttributes(lines, m);
            }
        }

        private void ExtractAttributes(List<String> lines, Meeting m)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("ATTENDEE;CUTYPE="))
                {
                    Person attendee = new Person();
                    String[] words = lines[i].Split(';');
                    for (int j = 0; j < words.Length; j++)
                    {
                        if (words[j].StartsWith("CN="))
                        {
                            String[] parts = words[j].Split('=');
                            if (parts.Length > 1)
                            {
                                attendee.name = parts[1];
                            }
                        }
                        else if (words[j].StartsWith("X-NUM-GUESTS"))
                        {
                            String[] part = words[j].Split(':');
                            if ((part.Length == 3) &&
                                (part[0].StartsWith("X-NUM-GUESTS")) &&
                                (part[1].StartsWith("mailto")))
                            {
                                attendee.email = part[2];
                            }
                        }
                    }

                    m.attendees.Add(attendee);
                }
                else if (lines[i].StartsWith("ORGANIZER"))
                {
                    Person organizer = new Person();
                    String[] words = lines[i].Split(';');
                    for (int j = 0; j < words.Length; j++)
                    {
                        if (words[j].StartsWith("CN="))
                        {
                            String[] part = words[j].Split(':');
                            if ((part.Length == 3) &&
                                (part[0].StartsWith("CN")) &&
                                (part[1].StartsWith("mailto")))
                            {
                                String[] subparts = part[0].Split('=');
                                if (subparts.Length > 1)
                                {
                                    organizer.name = subparts[1];
                                }

                                organizer.email = part[2];
                            }
                        }
                    }

                    m.organizer = organizer;
                }
                else if (lines[i].StartsWith("SUMMARY"))
                {
                    String[] words = lines[i].Split(':');
                    if ((words.Length == 2) &&
                        (words[0].StartsWith("SUMMARY")))
                    {
                        m.title = words[1];
                    }
                }
                else if (lines[i].StartsWith("LOCATION"))
                {
                    String[] words = lines[i].Split(':');
                    if ((words.Length == 2) &&
                        (words[0].StartsWith("LOCATION")))
                    {
                        m.location = words[1];
                    }
                }
                else if (lines[i].StartsWith("DTSTART"))
                {
                    String[] words = lines[i].Split(':');
                    if ((words.Length == 2) &&
                        (words[0].StartsWith("DTSTART")))
                    {
                        m.startDate = words[1];
                    }
                }
                else if (lines[i].StartsWith("DTEND"))
                {
                    String[] words = lines[i].Split(':');
                    if ((words.Length == 2) &&
                        (words[0].StartsWith("DTEND")))
                    {
                        m.endDate = words[1];
                    }
                }
            }
        }

        /// <summary>
        /// Example showing:
        ///  - how to fetch all messages from a POP3 server
        /// </summary>
        /// <param name="hostname">Hostname of the server. For example: pop3.live.com</param>
        /// <param name="port">Host port to connect to. Normally: 110 for plain POP3, 995 for SSL POP3</param>
        /// <param name="useSsl">Whether or not to use SSL to connect to server</param>
        /// <param name="username">Username of the user on the server</param>
        /// <param name="password">Password of the user on the server</param>
        /// <returns>All Messages on the POP3 server</returns>
        public List<Message> FetchAllMessages(string hostname, int port, bool useSsl, string username, string password)
        {
            // The client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                // Connect to the server
                client.Connect(hostname, port, useSsl);

                // Authenticate ourselves towards the server
                client.Authenticate(username, password);

                // Get the number of messages in the inbox
                int messageCount = client.GetMessageCount();

                // We want to download all messages
                List<Message> allMessages = new List<Message>(messageCount);
                List<string> uids = client.GetMessageUids();

                // Messages are numbered in the interval: [1, messageCount]
                // Ergo: message numbers are 1-based.
                for (int i = 1; i <= messageCount; i++)
                {
                    Message m = client.GetMessage(i);
                    if (m.MessagePart != null)
                    {
                        if (m.MessagePart.MessageParts != null)
                        {
                            for (int j = 0; j < m.MessagePart.MessageParts.Count; j++)
                            {
                                if ((m.MessagePart.MessageParts[j].IsAttachment) &&
                                    (m.MessagePart.MessageParts[j].FileName == "invite.ics"))
                                {
                                    System.Console.Write(ASCIIEncoding.ASCII.GetString(m.MessagePart.MessageParts[j].Body));
                                    ParseICalendar(m.MessagePart.MessageParts[j].Body);
                                }
                            }
                        }
                    }
                    //System.Console.Write(ASCIIEncoding.ASCII.GetString(m.MessagePart.MessageParts[0].Body));
                    allMessages.Add(m);
                }

                // Now return the fetched messages
                return allMessages;
            }
        }

        static void Main(string[] args)
        {
            MailboxScanner ms = new MailboxScanner();
        }
    }
}
