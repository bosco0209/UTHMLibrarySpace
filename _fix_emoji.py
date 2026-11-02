import io
p = r'UTHMLibrarySpace\Services\EmailService.cs'
s = io.open(p, encoding='utf-8').read()

reps = [
    ('"Approved" or "Confirmed" => ("#10b981", "#ecfdf5", "?")', '"Approved" or "Confirmed" => ("#10b981", "#ecfdf5", "✅")'),
    ('"Rejected" => ("#ef4444", "#fef2f2", "?")', '"Rejected" => ("#ef4444", "#fef2f2", "❌")'),
    ('"Cancelled" => ("#f59e0b", "#fffbeb", "??")', '"Cancelled" => ("#f59e0b", "#fffbeb", "⚠️")'),
    ('_ => ("#8b5cf6", "#faf5ff", "??")', '_ => ("#8b5cf6", "#faf5ff", "📅")'),
    ('"?? Your UTHM Library Space Verification Code: {code}"', '"🔐 Your UTHM Library Space Verification Code: {code}"'),
    ('"?? Password Reset Code: {code}"', '"🔑 Password Reset Code: {code}"'),
    ('<strong>?? Security Tip:', '<strong>🔒 Security Tip:'),
    ('<strong>?? Didn\'t request this?</strong>', '<strong>⚠️ Didn\'t request this?</strong>'),
    ('Hi {firstName} ??', 'Hi {firstName} 👋'),
    ('?? UTHM Library Space', '📚 UTHM Library Space'),
    ('?Your UTHM Library Space Verification Code', '🔐 Your UTHM Library Space Verification Code'),
]
# two ??</span> occurrences: line 134 (verification, uses book emoji) and line 305 (password, key emoji)
idx = s.find('??</span>')
if idx != -1:
    s = s[:idx] + '📚</span>' + s[idx + len('??</span>'):]
idx = s.find('??</span>')
if idx != -1:
    s = s[:idx] + '🔑</span>' + s[idx + len('??</span>'):]

for old, new in reps:
    s = s.replace(old, new)

io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('done')
