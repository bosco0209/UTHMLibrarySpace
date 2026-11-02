import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()
cur.execute("""INSERT INTO Facilities
   (Name, Code, Type, Capacity, Location, Description, Amenities, ImageUrl, Status, CapacityMin, Units, TypeName)
   VALUES ('Bilik Tutorial', 'BT-01', 'BilikTutorial', 20, 'ARAS 3, PTTA',
   'Bilik Tutorial — bilik tutorial milik pensyarah; hanya boleh ditempah oleh pensyarah.',
   'Papan putih,Projektor,Mikrofon,WiFi,Berhawa dingin',
   'https://images.unsplash.com/photo-1541829070764-84a7d30dd3f3?auto=format&fit=crop&w=1200&q=80',
   'Available', 10, 1, 'Bilik Tutorial')""")
cur.execute("INSERT INTO CategoryLimits (Type, MaxHoursPerBooking, MaxHoursPerWeek, MaxActiveBookings) VALUES ('BilikTutorial', 4, 12, 4)")
con.commit()
for r in cur.execute('select Id, Code, Name, Type, TypeName from Facilities order by Code'): print(r)
con.close()
