namespace BotGenerator.Core.Services;

/// <summary>
/// Provides variation in pre-determined bot responses for a more natural conversation.
/// Each method returns a random variation of a specific response type.
/// </summary>
public static class ResponseVariations
{
    private static readonly Random _random = new();

    /// <summary>
    /// Large group (>10 people) contact card response.
    /// </summary>
    public static string LargeGroupVCard() => Pick(new[]
    {
        "Te he enviado la tarjeta de contacto de nuestro equipo de reservas. Ellos te ayudarán con la reserva para tu grupo. ¡Gracias!",
        "Ahí tienes el contacto de nuestro equipo de reservas. Te atenderán personalmente para organizar tu grupo. ¡Un saludo!",
        "Te paso el contacto directo con nuestro equipo. Ellos se encargarán de tu reserva para el grupo. ¡Gracias por elegir Villa Carmen!",
        "Ya tienes la tarjeta de contacto. Nuestro equipo te ayudará con todos los detalles de la reserva. ¡Hasta pronto!",
        "Listo, te he enviado el contacto. Para grupos grandes te atenderán ellos directamente. ¡Gracias!",
        "Ahí va el contacto de reservas. Para grupos numerosos, ellos te darán mejor atención. ¡Gracias por contar con nosotros!",
        "Te envío la tarjeta del equipo de reservas. Para un grupo como el vuestro, os atenderán de forma personalizada.",
        "Aquí tienes el contacto. Nuestro equipo especializado en grupos te echará una mano. ¡Un saludo!",
        "Te he pasado la tarjeta. Ellos os organizarán todo para que la experiencia sea perfecta. ¡Gracias!",
        "Perfecto, ahí tienes el contacto directo. Para grupos os atienden ellos personalmente. ¡Nos vemos pronto!"
    });

    /// <summary>
    /// Special request (cakes, celebrations) contact card response.
    /// </summary>
    public static string SpecialRequestVCard() => Pick(new[]
    {
        "Te he enviado la tarjeta de contacto. Nuestro equipo te ayudará con tu solicitud especial. ¡Gracias!",
        "Ahí tienes el contacto. Para peticiones especiales, nuestro equipo te atenderá directamente. ¡Un saludo!",
        "Ya tienes la tarjeta. Ellos te ayudarán con todo lo que necesitas para tu celebración. ¡Gracias por confiar en nosotros!",
        "Te paso el contacto de nuestro equipo. Te ayudarán a preparar algo especial. ¡Hasta pronto!",
        "Listo, te he enviado el contacto. Para solicitudes como esta, te atenderán personalmente. ¡Gracias!",
        "Aquí tienes la tarjeta del equipo. Para ocasiones especiales, ellos os prepararán algo único.",
        "Te envío el contacto de nuestro equipo de eventos. Ellos harán que tu celebración sea perfecta.",
        "Ahí va la tarjeta. Nuestro equipo te ayudará a organizar todos los detalles especiales. ¡Un saludo!",
        "Perfecto, te paso el contacto. Para tartas y celebraciones, ellos os atenderán de maravilla.",
        "Ya tienes el contacto directo. Ellos se encargarán de que tu evento sea memorable. ¡Gracias!"
    });

    /// <summary>
    /// Asking for booking date.
    /// </summary>
    public static string AskDate() => Pick(new[]
    {
        "¿Para qué día os gustaría reservar?",
        "¿Qué día os viene bien?",
        "¿Para qué fecha sería?",
        "¿Qué día queréis venir?",
        "¿Para cuándo sería la reserva?",
        "¿Y para qué día queréis la mesa?",
        "¿Qué día preferís para venir?",
        "Dime, ¿para qué día sería?",
        "¿Qué día tenéis pensado?",
        "¿Para cuándo os reservo mesa?"
    });

    /// <summary>
    /// Asking for booking time.
    /// </summary>
    public static string AskTime() => Pick(new[]
    {
        "¿A qué hora os gustaría reservar?",
        "¿A qué hora os viene bien?",
        "¿A qué hora queréis venir?",
        "¿Qué hora os vendría mejor?",
        "¿Para qué hora sería?",
        "¿Y a qué hora os espero?",
        "¿Qué hora preferís?",
        "Dime, ¿a qué hora sería?",
        "¿A qué hora tenéis pensado venir?",
        "¿Para qué hora os va bien?"
    });

    /// <summary>
    /// Asking about highchairs.
    /// </summary>
    public static string AskTronas() => Pick(new[]
    {
        "Antes de confirmarla, ¿necesitáis *tronas*?",
        "¿Vais a necesitar *tronas* para los peques?",
        "¿Necesitáis alguna *trona* para niños?",
        "¿Necesitáis *tronas*?",
        "¿Os preparo alguna *trona*?",
        "¿Venís con niños pequeños? ¿Necesitáis *tronas*?",
        "¿Traéis peques? ¿Os pongo *tronas*?",
        "Una cosita, ¿necesitáis *tronas* para los niños?",
        "¿Queréis que os prepare alguna *trona*?",
        "¿Lleváis niños que necesiten *trona*?"
    });

    /// <summary>
    /// Asking about strollers.
    /// </summary>
    public static string AskCarritos() => Pick(new[]
    {
        "¿Vais a traer *carrito de bebé*?",
        "¿Traéis *carrito de bebé*?",
        "¿Venís con *carrito de bebé*?",
        "¿Necesitáis espacio para *carrito de bebé*?",
        "¿Lleváis *carrito*?",
        "¿Os reservo sitio para *carrito de bebé*?",
        "¿Venís con algún *carrito*?",
        "¿Traéis *carrito* para los peques?",
        "¿Necesitáis aparcar algún *carrito*?",
        "¿Hay que dejar espacio para *carrito de bebé*?"
    });

    /// <summary>
    /// Asking about rice.
    /// </summary>
    public static string AskRice() => Pick(new[]
    {
        "Una cosa más: ¿queréis *arroz*? (sí/no)",
        "¿Os apetece *arroz*?",
        "¿Queréis añadir *arroz* a la reserva?",
        "¿Os preparo *arroz*?",
        "¿Vais a querer *arroz*?",
        "Por cierto, ¿os apetece pedir *arroz*?",
        "¿Habéis pensado si queréis *arroz*?",
        "¿Os gustaría añadir *arroz* a vuestra reserva?",
        "Y una cosita más: ¿queréis *arroz*?",
        "¿Queréis que os reserve *arroz*?"
    });

    /// <summary>
    /// Asking for rice portions.
    /// </summary>
    public static string AskRicePortions() => Pick(new[]
    {
        "Perfecto. ¿Cuántas *raciones* de arroz queréis? (mínimo 2)",
        "¿Cuántas *raciones* os preparo? (mínimo 2)",
        "¿Cuántas *raciones* queréis? Recordad que el mínimo son 2.",
        "Genial. ¿Cuántas *raciones* necesitáis? (mínimo 2)",
        "¿Y cuántas *raciones* serían? (mínimo 2)",
        "Vale, ¿cuántas *raciones* de arroz? (mínimo 2)",
        "¿Cuántas *raciones* os pongo? El mínimo son 2.",
        "Estupendo. ¿Cuántas *raciones*? (mínimo 2)",
        "¿Para cuántas *raciones* de arroz? (mínimo 2)",
        "Muy bien. ¿Cuántas *raciones* queréis? (mínimo 2)"
    });

    /// <summary>
    /// Asking how many highchairs.
    /// </summary>
    public static string AskTronasCount() => Pick(new[]
    {
        "Perfecto. ¿Cuántas tronas necesitáis?",
        "Vale. ¿Cuántas tronas os preparo?",
        "¿Cuántas tronas necesitáis?",
        "Genial. ¿Cuántas serían?",
        "De acuerdo. ¿Cuántas tronas?",
        "Estupendo. ¿Cuántas tronas os pongo?",
        "Muy bien. ¿Cuántas tronas queréis?",
        "¿Y cuántas tronas serían?",
        "Entendido. ¿Cuántas tronas necesitáis?",
        "¿Cuántas tronas os reservo?"
    });

    /// <summary>
    /// Asking how many strollers.
    /// </summary>
    public static string AskCarritosCount() => Pick(new[]
    {
        "Vale. ¿Cuántos carritos vais a traer?",
        "¿Cuántos carritos traéis?",
        "De acuerdo. ¿Cuántos carritos serían?",
        "¿Cuántos carritos necesitáis aparcar?",
        "Perfecto. ¿Cuántos carritos?",
        "Entendido. ¿Cuántos carritos lleváis?",
        "¿Y cuántos carritos traéis?",
        "Muy bien. ¿Cuántos carritos serían?",
        "¿Cuántos carritos os reservo sitio?",
        "Genial. ¿Cuántos carritos venís?"
    });

    /// <summary>
    /// Greeting response.
    /// </summary>
    public static string Greeting(string name) => Pick(new[]
    {
        $"¡Hola {name}! ¿Quieres hacer una reserva?",
        $"¡Hola {name}! ¿En qué puedo ayudarte?",
        $"¡Buenas {name}! ¿Te ayudo con una reserva?",
        $"¡Hola {name}! ¿Qué tal? ¿Quieres reservar mesa?",
        $"¡Hola {name}! Encantado de saludarte. ¿En qué te ayudo?",
        $"¡Buenas {name}! ¿Necesitas ayuda con una reserva?",
        $"¡Hola {name}! ¿Qué te trae por aquí?",
        $"¡Hola {name}! Bienvenido a Villa Carmen. ¿En qué puedo ayudarte?",
        $"¡Qué tal {name}! ¿Te echo una mano con la reserva?",
        $"¡Hola {name}! Un placer saludarte. ¿Quieres reservar?"
    });

    /// <summary>
    /// Maximum tronas/carritos warning.
    /// </summary>
    public static string MaxTronas() => Pick(new[]
    {
        "Podemos preparar como máximo *3* tronas. ¿Cuántas necesitáis?",
        "Solo disponemos de *3* tronas como máximo. ¿Cuántas os preparo?",
        "Tenemos un máximo de *3* tronas. ¿Cuántas necesitáis?",
        "El máximo son *3* tronas. ¿Cuántas queréis?",
        "Disponemos de hasta *3* tronas. ¿Cuántas os reservo?",
        "Lo siento, el límite son *3* tronas. ¿Cuántas os pongo?",
        "Como máximo podemos preparar *3* tronas. ¿Cuántas serían?",
        "Perdona, solo tenemos *3* tronas disponibles. ¿Cuántas queréis?",
        "Nuestro máximo es *3* tronas. ¿Cuántas necesitáis?",
        "Tenemos tope de *3* tronas. ¿Cuántas os preparo?"
    });

    /// <summary>
    /// Maximum carritos warning.
    /// </summary>
    public static string MaxCarritos() => Pick(new[]
    {
        "Podemos gestionar como máximo *3* carritos. ¿Cuántos vais a traer?",
        "Solo podemos acomodar *3* carritos. ¿Cuántos traéis?",
        "Tenemos espacio para un máximo de *3* carritos. ¿Cuántos serían?",
        "El máximo son *3* carritos. ¿Cuántos necesitáis?",
        "Disponemos de espacio para *3* carritos como máximo. ¿Cuántos?",
        "Lo siento, el límite son *3* carritos. ¿Cuántos traéis?",
        "Como máximo tenemos sitio para *3* carritos. ¿Cuántos serían?",
        "Perdona, solo podemos aparcar *3* carritos. ¿Cuántos venís?",
        "Nuestro máximo es *3* carritos. ¿Cuántos lleváis?",
        "Tenemos tope de *3* carritos. ¿Cuántos necesitáis?"
    });

    /// <summary>
    /// Minimum rice portions warning.
    /// </summary>
    public static string MinRicePortions() => Pick(new[]
    {
        "Para los arroces el mínimo es *2 raciones*. ¿Cuántas queréis?",
        "El mínimo de arroz son *2 raciones*. ¿Cuántas os preparo?",
        "Servimos el arroz en mínimo *2 raciones*. ¿Cuántas necesitáis?",
        "Nuestros arroces van de *2 raciones* en adelante. ¿Cuántas queréis?",
        "El arroz sale en mínimo *2 raciones*. ¿Cuántas os pongo?",
        "Lo siento, el arroz se sirve mínimo *2 raciones*. ¿Cuántas serían?",
        "Perdona, el mínimo son *2 raciones* de arroz. ¿Cuántas queréis?",
        "El arroz tiene un mínimo de *2 raciones*. ¿Cuántas os preparo?",
        "Como mínimo son *2 raciones* de arroz. ¿Cuántas necesitáis?",
        "Preparamos el arroz desde *2 raciones*. ¿Cuántas os pongo?"
    });

    // ========== MODIFICATION FLOW RESPONSES ==========

    /// <summary>
    /// No future bookings found for this phone number.
    /// </summary>
    public static string ModificationNoBookingsFound() => Pick(new[]
    {
        "No he encontrado ninguna reserva futura con este número de teléfono. ¿Quieres hacer una nueva reserva?",
        "No tengo reservas pendientes asociadas a este teléfono. ¿Te ayudo a crear una reserva nueva?",
        "No encuentro reservas futuras con tu número. ¿Necesitas hacer una reserva?",
        "Vaya, no hay ninguna reserva próxima con este teléfono. ¿Quieres reservar mesa?",
        "No aparece ninguna reserva futura tuya. ¿Hacemos una reserva nueva?",
        "No tengo constancia de reservas pendientes con este número. ¿Te ayudo a reservar?",
        "No encuentro ninguna reserva activa con tu teléfono. ¿Quieres que te reserve mesa?",
        "Mmm, no veo reservas futuras asociadas a tu número. ¿Hacemos una nueva?",
        "No hay reservas pendientes con este teléfono. ¿Reservamos mesa?",
        "No tengo reservas registradas con este número para próximas fechas. ¿Te ayudo con una nueva?"
    });

    /// <summary>
    /// Asking which booking to modify when multiple exist.
    /// </summary>
    public static string ModificationSelectBooking() => Pick(new[]
    {
        "¿Cuál de estas reservas quieres modificar?",
        "Dime cuál de las reservas te gustaría cambiar.",
        "¿Qué reserva necesitas modificar?",
        "¿Cuál de ellas quieres modificar?",
        "Indícame cuál reserva quieres cambiar.",
        "¿Sobre cuál de las reservas hacemos el cambio?",
        "¿Qué reserva te gustaría modificar?",
        "Dime, ¿cuál de estas reservas necesitas cambiar?",
        "¿Cuál es la reserva que quieres modificar?",
        "¿En cuál de estas reservas hacemos el cambio?"
    });

    /// <summary>
    /// Asking what field to modify.
    /// </summary>
    public static string ModificationSelectField() => Pick(new[]
    {
        "¿Qué quieres modificar de tu reserva?",
        "¿Qué te gustaría cambiar?",
        "Dime qué quieres modificar.",
        "¿Qué necesitas cambiar de la reserva?",
        "¿Qué parte de la reserva modificamos?",
        "¿Qué cambio necesitas hacer?",
        "¿Qué te gustaría modificar de tu reserva?",
        "Cuéntame, ¿qué quieres cambiar?",
        "¿Qué aspecto de la reserva cambiamos?",
        "¿En qué te ayudo con la modificación?"
    });

    /// <summary>
    /// Asking for new date.
    /// </summary>
    public static string ModificationAskNewDate() => Pick(new[]
    {
        "¿Para qué nuevo día quieres la reserva?",
        "¿A qué fecha lo movemos?",
        "Dime la nueva fecha que prefieres.",
        "¿Para qué día lo cambiamos?",
        "¿Qué día os viene mejor?",
        "¿A qué fecha quieres cambiar?",
        "¿Para cuándo lo movemos?",
        "¿Qué nueva fecha te gustaría?",
        "Dime el nuevo día para la reserva.",
        "¿A qué fecha lo pasamos?"
    });

    /// <summary>
    /// Asking for new time.
    /// </summary>
    public static string ModificationAskNewTime() => Pick(new[]
    {
        "¿A qué nueva hora os va mejor?",
        "¿A qué hora lo cambiamos?",
        "Dime la nueva hora que prefieres.",
        "¿A qué hora te gustaría cambiarlo?",
        "¿Qué hora os viene mejor?",
        "¿A qué hora lo movemos?",
        "¿Para qué hora lo cambiamos?",
        "Dime la nueva hora para la reserva.",
        "¿A qué hora queréis venir?",
        "¿Qué nueva hora preferís?"
    });

    /// <summary>
    /// Asking for new party size.
    /// </summary>
    public static string ModificationAskNewPartySize() => Pick(new[]
    {
        "¿Cuántas personas seréis ahora?",
        "¿A cuántos comensales lo cambiamos?",
        "Dime el nuevo número de personas.",
        "¿Cuántos seréis finalmente?",
        "¿Para cuántas personas lo cambio?",
        "¿Cuántas personas vendréis?",
        "¿A cuántos comensales actualizamos?",
        "¿Cuántos seréis ahora?",
        "Dime cuántas personas vendréis.",
        "¿Para cuántos lo actualizo?"
    });

    /// <summary>
    /// Asking for rice change details.
    /// </summary>
    public static string ModificationAskNewRice() => Pick(new[]
    {
        "¿Qué cambio quieres hacer con el arroz? Puedes cambiar el tipo, las raciones, o cancelarlo.",
        "¿Qué modificación necesitas en el arroz? (tipo, raciones, o cancelar)",
        "Dime qué quieres cambiar del arroz: tipo, raciones, o cancelarlo.",
        "¿Qué ajuste hacemos con el arroz? ¿Tipo, raciones, o lo quitamos?",
        "¿Qué cambio necesitas en el arroz? Puedes modificar tipo, raciones, o cancelarlo.",
        "¿Cómo modificamos el arroz? (cambiar tipo, raciones, o cancelar)",
        "¿Qué hacemos con el arroz? ¿Cambio de tipo, raciones, o lo quitamos?",
        "Cuéntame qué cambio necesitas en el arroz.",
        "¿Qué quieres modificar del arroz? Tipo, raciones, o cancelarlo.",
        "¿Qué ajustamos del arroz? Puedes cambiar tipo, raciones, o quitarlo."
    });

    /// <summary>
    /// Date unavailable, suggesting alternatives.
    /// </summary>
    public static string ModificationDateUnavailable() => Pick(new[]
    {
        "Lo siento, esa fecha no está disponible para tu grupo. Te sugiero estas alternativas:",
        "Vaya, esa fecha está completa. Aquí tienes otras opciones:",
        "No hay disponibilidad para esa fecha. ¿Te viene bien alguna de estas?",
        "Esa fecha no tiene hueco disponible. Te propongo estas alternativas:",
        "Lo siento, esa fecha no está disponible. ¿Alguna de estas te viene bien?",
        "No queda disponibilidad para esa fecha. Prueba con alguna de estas:",
        "Vaya, no hay hueco para ese día. Te sugiero estas opciones:",
        "Esa fecha está llena. ¿Te interesa alguna de estas alternativas?",
        "No hay disponibilidad ese día. ¿Qué tal alguna de estas opciones?",
        "Lo siento, no hay sitio para esa fecha. Te ofrezco estas alternativas:"
    });

    /// <summary>
    /// Time unavailable, suggesting alternatives.
    /// </summary>
    public static string ModificationTimeUnavailable() => Pick(new[]
    {
        "Lo siento, esa hora no está disponible. Te sugiero estas alternativas:",
        "Vaya, a esa hora está completo. Aquí tienes otras opciones:",
        "No hay disponibilidad a esa hora. ¿Te viene bien alguna de estas?",
        "Esa hora no tiene hueco. Te propongo estas alternativas:",
        "Lo siento, esa hora no está disponible. ¿Alguna de estas te va bien?",
        "No queda disponibilidad a esa hora. Prueba con alguna de estas:",
        "Vaya, no hay hueco a esa hora. Te sugiero estas opciones:",
        "Esa hora está llena. ¿Te interesa alguna de estas alternativas?",
        "No hay disponibilidad a esa hora. ¿Qué tal alguna de estas opciones?",
        "Lo siento, no hay sitio a esa hora. Te ofrezco estas alternativas:"
    });

    /// <summary>
    /// Modification successful.
    /// </summary>
    public static string ModificationSuccess() => Pick(new[]
    {
        "¡Perfecto! Tu reserva ha sido modificada correctamente. ¡Nos vemos pronto!",
        "¡Listo! He actualizado tu reserva. ¡Te esperamos!",
        "¡Hecho! La modificación se ha guardado. ¡Hasta pronto!",
        "¡Genial! Tu reserva ya está actualizada. ¡Nos vemos!",
        "¡Perfecto! He aplicado los cambios a tu reserva. ¡Te esperamos!",
        "¡Todo listo! La reserva ha sido modificada. ¡Hasta pronto!",
        "¡Modificación completada! Tu reserva está actualizada. ¡Nos vemos!",
        "¡Hecho! Los cambios han sido aplicados. ¡Te esperamos!",
        "¡Perfecto! Tu reserva ya refleja los cambios. ¡Hasta pronto!",
        "¡Genial! La modificación se ha realizado correctamente. ¡Nos vemos!"
    });

    /// <summary>
    /// Modification cancelled by user.
    /// </summary>
    public static string ModificationCancelled() => Pick(new[]
    {
        "De acuerdo, no hago ningún cambio. Tu reserva sigue igual. ¿Te ayudo con algo más?",
        "Vale, cancelo la modificación. Tu reserva queda como estaba. ¿Necesitas algo más?",
        "Entendido, no modifico nada. ¿Te puedo ayudar con otra cosa?",
        "Ok, dejamos la reserva como está. ¿En qué más te puedo ayudar?",
        "Perfecto, no hago cambios. Tu reserva sigue igual. ¿Algo más?",
        "De acuerdo, no hacemos el cambio. ¿Te ayudo con algo más?",
        "Vale, cancelamos la modificación. ¿Necesitas algo más?",
        "Entendido, dejamos todo como estaba. ¿Te puedo ayudar en algo?",
        "Ok, no hago ningún cambio. ¿En qué más puedo ayudarte?",
        "Perfecto, la reserva queda igual. ¿Necesitas ayuda con algo más?"
    });

    /// <summary>
    /// Large group modification (>10 people) - sending contact card.
    /// </summary>
    public static string ModificationLargeGroupVCard() => Pick(new[]
    {
        "Para grupos de más de 10 personas, te paso el contacto de nuestro equipo de reservas. Ellos te ayudarán con el cambio.",
        "Los cambios para grupos grandes los gestiona nuestro equipo directamente. Te envío su contacto.",
        "Para más de 10 personas, te pongo en contacto con nuestro equipo de reservas. Te envío la tarjeta.",
        "Para grupos numerosos, nuestro equipo te atenderá mejor. Te paso su contacto.",
        "Los grupos de más de 10 personas los gestionamos de forma personalizada. Te envío el contacto.",
        "Para ese número de personas, te paso el contacto de nuestro equipo especializado.",
        "Para grupos grandes te atiende nuestro equipo de reservas. Aquí tienes su contacto.",
        "Para más de 10 personas, mejor contacta directamente con nuestro equipo. Te paso el contacto.",
        "Los cambios para grupos numerosos los gestiona nuestro equipo. Te envío su tarjeta.",
        "Para grupos de este tamaño, te pongo en contacto con nuestro equipo de reservas."
    });

    /// <summary>
    /// Unsupported modification request (media/audio/complex) - continuing conversation.
    /// </summary>
    public static string ModificationUnsupportedRequest() => Pick(new[]
    {
        "Para este tipo de solicitud, te paso el contacto de nuestro equipo. Ellos te ayudarán mejor. ¿En qué más puedo ayudarte?",
        "Eso lo gestiona mejor nuestro equipo directamente. Te envío su contacto por si necesitas hablar con ellos. ¿Algo más?",
        "Para esa solicitud, te recomiendo contactar con nuestro equipo. Te paso su tarjeta. ¿Te ayudo con algo más?",
        "Eso escapa un poco de lo que puedo hacer yo. Te paso el contacto de nuestro equipo. ¿Necesitas algo más?",
        "Para eso es mejor que hables con nuestro equipo. Te envío su contacto. ¿En qué más te puedo ayudar?",
        "Esa solicitud la gestiona mejor nuestro equipo. Aquí tienes su contacto. ¿Algo más en lo que pueda ayudarte?",
        "Para eso te paso el contacto de nuestro equipo de reservas. ¿Necesitas ayuda con algo más?",
        "Eso lo resuelve mejor nuestro equipo directamente. Te paso su tarjeta. ¿Te ayudo con otra cosa?",
        "Para solicitudes así, te pongo en contacto con nuestro equipo. ¿En qué más puedo ayudarte?",
        "Eso es mejor gestionarlo con nuestro equipo. Te envío su contacto. ¿Algo más?"
    });

    /// <summary>
    /// Confirmation prompt for modification changes.
    /// </summary>
    public static string ModificationConfirmChanges() => Pick(new[]
    {
        "¿Confirmas este cambio?",
        "¿Te parece bien? Confirma con 'sí' para aplicar el cambio.",
        "¿Confirmamos el cambio?",
        "¿Está todo correcto? Di 'sí' para confirmar.",
        "¿Lo confirmamos?",
        "¿Procedo con el cambio?",
        "¿Confirmamos esta modificación?",
        "Si está todo bien, confirma con 'sí'.",
        "¿Aplicamos el cambio?",
        "¿Confirmas la modificación?"
    });

    /// <summary>
    /// Asking for number of tronas in modification.
    /// </summary>
    public static string ModificationAskTronas() => Pick(new[]
    {
        "¿Cuántas tronas necesitáis ahora?",
        "¿A cuántas tronas lo cambio?",
        "Dime cuántas tronas necesitáis.",
        "¿Cuántas tronas queréis?",
        "¿Cuántas tronas os preparo?",
        "¿A cuántas tronas actualizamos?",
        "Dime el nuevo número de tronas.",
        "¿Cuántas tronas van a ser?",
        "¿Para cuántas tronas lo cambio?",
        "¿Cuántas tronas necesitáis finalmente?"
    });

    /// <summary>
    /// Asking for number of carritos in modification.
    /// </summary>
    public static string ModificationAskCarritos() => Pick(new[]
    {
        "¿Cuántos carritos vais a traer ahora?",
        "¿A cuántos carritos lo cambio?",
        "Dime cuántos carritos traeréis.",
        "¿Cuántos carritos necesitáis?",
        "¿Cuántos carritos os reservo sitio?",
        "¿A cuántos carritos actualizamos?",
        "Dime el nuevo número de carritos.",
        "¿Cuántos carritos van a ser?",
        "¿Para cuántos carritos lo cambio?",
        "¿Cuántos carritos traeréis finalmente?"
    });

    // ========== MODIFICATION ERROR RESPONSES ==========

    /// <summary>
    /// Booking selection not understood (multiple bookings exist).
    /// </summary>
    public static string BookingSelectionNotUnderstood() => Pick(new[]
    {
        "No entendí cuál reserva quieres. Por favor, indica el número (1, 2, 3...) o describe cuál (\"la del sábado\", \"la de 6 personas\", etc.)",
        "Perdona, no pillé cuál reserva dices. ¿Puedes indicarme el número o describirla?",
        "No me quedó claro cuál reserva quieres. Dime el número o descríbela (ej: \"la primera\", \"la del viernes\").",
        "¿Cuál de las reservas? Puedes decirme el número o describir cuál (\"la de las 14:00\", \"la de 4 personas\").",
        "No entendí bien. Dime qué reserva quieres: el número (1, 2...) o una descripción.",
        "Perdona, ¿cuál de las reservas? Indica el número o descríbela.",
        "No me ha quedado claro. ¿Me dices el número de la reserva o la describes?",
        "¿Cuál reserva dices? Puedes usar el número o describir (\"la del domingo\", \"la segunda\").",
        "No entendí cuál. Dime el número (1, 2, 3...) o descríbela (día, hora, personas...).",
        "Perdona, no pillé cuál. ¿Me indicas el número o me la describes?"
    });

    /// <summary>
    /// Field selection not understood in modification.
    /// </summary>
    public static string FieldSelectionNotUnderstood() => Pick(new[]
    {
        "No entendí qué quieres modificar. Por favor elige:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "Perdona, no pillé qué quieres cambiar. Elige una opción:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "No me quedó claro qué modificar. Dime qué cambiar:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "¿Qué parte quieres cambiar? Elige:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "No entendí bien. ¿Qué modificamos?\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "Perdona, ¿qué quieres cambiar?\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "No me ha quedado claro. Dime qué cambiar:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "¿Qué necesitas modificar? Opciones:\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "No pillé qué cambiar. ¿Qué modificamos?\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos",
        "¿Qué aspecto cambiamos?\n1️⃣ Fecha\n2️⃣ Hora\n3️⃣ Personas\n4️⃣ Arroz\n5️⃣ Tronas\n6️⃣ Carritos"
    });

    /// <summary>
    /// Unknown error during modification - restart needed.
    /// </summary>
    public static string ModificationUnknownError() => Pick(new[]
    {
        "Ha ocurrido un error. Por favor, empieza de nuevo diciendo que quieres modificar tu reserva.",
        "Vaya, algo ha fallado. Vuelve a indicarme que quieres modificar la reserva.",
        "Ups, ha habido un problema. Dime otra vez que quieres modificar la reserva.",
        "Lo siento, algo salió mal. Por favor, vuelve a decirme que quieres modificar.",
        "Ha habido un error. Empieza de nuevo indicando que quieres modificar tu reserva.",
        "Perdona, algo falló. Di otra vez que quieres modificar la reserva.",
        "Vaya, hubo un problema. Vuelve a indicar que quieres modificar.",
        "Lo siento, ha ocurrido un error. Empieza de nuevo con la modificación.",
        "Ups, algo no fue bien. Dime otra vez que quieres modificar tu reserva.",
        "Ha fallado algo. Por favor, vuelve a empezar diciendo que quieres modificar."
    });

    /// <summary>
    /// Database save error during modification.
    /// </summary>
    public static string ModificationSaveError() => Pick(new[]
    {
        "Lo siento, hubo un error al guardar los cambios. Por favor, inténtalo de nuevo.",
        "Vaya, no se pudieron guardar los cambios. ¿Puedes intentarlo otra vez?",
        "Ups, hubo un problema al guardar. Por favor, inténtalo de nuevo.",
        "Lo siento, falló al guardar los cambios. Vuelve a intentarlo.",
        "No se han podido guardar los cambios. Por favor, inténtalo otra vez.",
        "Perdona, hubo un error guardando. ¿Puedes volver a intentarlo?",
        "Vaya, algo falló al guardar. Inténtalo de nuevo, por favor.",
        "Lo siento, los cambios no se guardaron. Por favor, vuelve a intentarlo.",
        "Hubo un problema al aplicar los cambios. Inténtalo otra vez.",
        "Ups, no se pudieron guardar. Por favor, inténtalo de nuevo."
    });

    /// <summary>
    /// Confirmation yes/no not understood.
    /// </summary>
    public static string ConfirmationNotUnderstood() => Pick(new[]
    {
        "Por favor, confirma con *Sí* o cancela con *No*.",
        "No entendí. ¿Confirmas? Responde *Sí* o *No*.",
        "¿Sí o no? No me quedó claro tu respuesta.",
        "Perdona, no pillé tu respuesta. ¿*Sí* para confirmar o *No* para cancelar?",
        "¿Confirmas? Dime *Sí* o *No*.",
        "No entendí bien. Responde *Sí* para confirmar o *No* para cancelar.",
        "¿Qué me dices? *Sí* para adelante o *No* para dejarlo.",
        "Por favor, dime *Sí* para confirmar o *No* para cancelar.",
        "No me quedó claro. ¿*Sí* o *No*?",
        "¿Confirmas o no? Responde *Sí* o *No*."
    });

    /// <summary>
    /// Date input not understood.
    /// </summary>
    public static string DateNotUnderstood() => Pick(new[]
    {
        "No entendí la fecha. Por favor, indica el día (ej: \"el sábado\", \"21/12\", \"21 de diciembre\")",
        "Perdona, no pillé la fecha. ¿Puedes decirme el día? (ej: \"viernes\", \"15/01\")",
        "No me quedó clara la fecha. Dímela de otra forma (ej: \"el domingo\", \"22 de enero\")",
        "¿Qué día dices? No lo entendí. Prueba con \"el jueves\" o \"20/12\" o \"20 de diciembre\".",
        "No entendí bien el día. ¿Me lo repites? (ej: \"sábado\", \"25/12\")",
        "Perdona, no capté la fecha. Dime el día de otra forma.",
        "No pillé la fecha. ¿Puedes indicarla como \"el viernes\" o \"18/01\"?",
        "No me quedó claro el día. Indícalo como \"el lunes\", \"15/12\" o \"15 de diciembre\".",
        "¿Qué fecha? No la entendí. Prueba con el nombre del día o formato dd/mm.",
        "No entendí qué día. Dímelo como \"el sábado\", \"21/12\" o \"21 de diciembre\"."
    });

    /// <summary>
    /// Time input not understood.
    /// </summary>
    public static string TimeNotUnderstood() => Pick(new[]
    {
        "No entendí la hora. Por favor, indica la hora (ej: \"14:00\", \"a las 15:30\")",
        "Perdona, no pillé la hora. ¿Puedes decírmela? (ej: \"15:00\", \"a las dos\")",
        "No me quedó clara la hora. Dímela de otra forma (ej: \"14:30\", \"a las tres\")",
        "¿Qué hora dices? No la entendí. Prueba con \"14:00\" o \"a las 14:00\".",
        "No entendí bien la hora. ¿Me la repites? (ej: \"15:30\")",
        "Perdona, no capté la hora. Dímela de otra forma.",
        "No pillé la hora. ¿Puedes indicarla como \"14:00\" o \"a las 15:00\"?",
        "No me quedó clara la hora. Indícala como \"14:30\" o \"a las dos y media\".",
        "¿A qué hora? No la entendí. Prueba con formato HH:MM.",
        "No entendí qué hora. Dímela como \"14:00\", \"15:30\" o \"a las dos\"."
    });

    /// <summary>
    /// Party size input not understood.
    /// </summary>
    public static string PartySizeNotUnderstood() => Pick(new[]
    {
        "No entendí el número de personas. Por favor, indica cuántas personas seréis.",
        "Perdona, no pillé cuántos seréis. ¿Puedes decirme el número?",
        "No me quedó claro cuántas personas. Dime el número (ej: \"4 personas\", \"seremos 6\").",
        "¿Cuántas personas? No lo entendí. Indica un número.",
        "No entendí bien cuántos seréis. ¿Me lo repites?",
        "Perdona, no capté el número de personas. ¿Cuántos seréis?",
        "No pillé cuántos sois. ¿Puedes indicar el número de personas?",
        "No me quedó claro. ¿Cuántas personas seréis?",
        "¿Cuántos comensales? No entendí. Dime el número.",
        "No entendí cuántos. Indica el número de personas (ej: \"4\", \"seremos 5\")."
    });

    /// <summary>
    /// Rice servings exceed party size.
    /// </summary>
    public static string RiceServingsExceedPartySize(int partySize) => Pick(new[]
    {
        $"El máximo de raciones es {partySize} (número de comensales). ¿Cuántas raciones quieres?",
        $"Solo podéis pedir hasta {partySize} raciones (sois {partySize}). ¿Cuántas queréis?",
        $"El límite son {partySize} raciones (una por persona). ¿Cuántas os pongo?",
        $"Como máximo pueden ser {partySize} raciones. ¿Cuántas quieres?",
        $"No podéis pedir más de {partySize} raciones (sois {partySize}). ¿Cuántas?",
        $"El tope son {partySize} raciones. ¿Cuántas os preparo?",
        $"Máximo {partySize} raciones (igual que comensales). ¿Cuántas queréis?",
        $"El máximo es {partySize} raciones. ¿Cuántas os pongo?",
        $"Solo hasta {partySize} raciones. ¿Cuántas quieres?",
        $"Podéis pedir hasta {partySize} raciones como máximo. ¿Cuántas?"
    });

    /// <summary>
    /// Tronas count not understood.
    /// </summary>
    public static string TronasNotUnderstood() => Pick(new[]
    {
        "No entendí cuántas tronas necesitas. Por favor, indica el número (0-3).",
        "Perdona, no pillé cuántas tronas. ¿Puedes decirme el número? (máximo 3)",
        "No me quedó claro. ¿Cuántas tronas necesitáis? (0-3)",
        "¿Cuántas tronas? No lo entendí. Indica un número del 0 al 3.",
        "No entendí bien. ¿Cuántas tronas queréis? (máximo 3)",
        "Perdona, no capté el número de tronas. ¿Cuántas? (0-3)",
        "No pillé cuántas tronas. Dime un número entre 0 y 3.",
        "No me quedó claro cuántas tronas. ¿Me lo repites?",
        "¿Cuántas tronas decías? Indica el número (0-3).",
        "No entendí cuántas tronas. Dime un número del 0 al 3."
    });

    /// <summary>
    /// Carritos count not understood.
    /// </summary>
    public static string CarritosNotUnderstood() => Pick(new[]
    {
        "No entendí cuántos carritos traes. Por favor, indica el número (0-3).",
        "Perdona, no pillé cuántos carritos. ¿Puedes decirme el número? (máximo 3)",
        "No me quedó claro. ¿Cuántos carritos traeréis? (0-3)",
        "¿Cuántos carritos? No lo entendí. Indica un número del 0 al 3.",
        "No entendí bien. ¿Cuántos carritos traéis? (máximo 3)",
        "Perdona, no capté el número de carritos. ¿Cuántos? (0-3)",
        "No pillé cuántos carritos. Dime un número entre 0 y 3.",
        "No me quedó claro cuántos carritos. ¿Me lo repites?",
        "¿Cuántos carritos decías? Indica el número (0-3).",
        "No entendí cuántos carritos. Dime un número del 0 al 3."
    });

    // ========== CANCELLATION FLOW RESPONSES ==========

    /// <summary>
    /// No future bookings found for cancellation.
    /// </summary>
    public static string CancellationNoBookingsFound() => Pick(new[]
    {
        "No encuentro ninguna reserva a tu nombre. ¿Quieres hacer una nueva?",
        "Vaya, no veo reservas futuras con este teléfono. ¿Hacemos una reserva?",
        "No tengo reservas registradas para ti. ¿Te ayudo a hacer una?",
        "Mmm, no encuentro ninguna reserva tuya. ¿Quieres reservar mesa?",
        "No hay reservas asociadas a este número. ¿Hacemos una nueva?",
        "No veo ninguna reserva a tu nombre. ¿Te ayudo a reservar?",
        "Parece que no tienes reservas pendientes. ¿Quieres hacer una?",
        "No encuentro reservas con tu teléfono. ¿Reservamos mesa?",
        "No hay reservas futuras para ti. ¿Te gustaría hacer una?",
        "No tienes reservas activas. ¿Quieres que hagamos una?"
    });

    /// <summary>
    /// Asking which booking to cancel when multiple exist.
    /// </summary>
    public static string CancellationSelectBooking() => Pick(new[]
    {
        "Tienes varias reservas. ¿Cuál quieres cancelar?",
        "Veo que tienes más de una reserva. ¿Cuál cancelamos?",
        "Tienes estas reservas pendientes. ¿Cuál quieres cancelar?",
        "Hay varias reservas a tu nombre. ¿Cuál deseas cancelar?",
        "Tienes múltiples reservas. Dime cuál quieres cancelar.",
        "Veo varias reservas tuyas. ¿Cuál cancelamos?",
        "Tienes estas reservas. ¿Cuál quieres que cancele?",
        "Hay más de una reserva. ¿Cuál es la que quieres cancelar?",
        "Tienes varias reservas activas. ¿Cuál cancelo?",
        "Veo múltiples reservas. ¿Cuál quieres cancelar?"
    });

    /// <summary>
    /// Confirmation prompt for cancellation.
    /// </summary>
    public static string CancellationConfirmPrompt() => Pick(new[]
    {
        "¿Estás seguro/a de que quieres cancelar esta reserva?",
        "¿Confirmas que quieres cancelar? Responde sí o no.",
        "¿Seguro que quieres cancelar esta reserva?",
        "¿De verdad quieres cancelar? Dime sí para confirmar.",
        "¿Cancelamos esta reserva? Responde sí o no.",
        "¿Confirmo la cancelación? Dime sí o no.",
        "¿Quieres que cancele esta reserva? Sí o no.",
        "¿Estás seguro? Una vez cancelada no se puede recuperar.",
        "¿Confirmas la cancelación de esta reserva?",
        "¿Seguro? Responde sí para cancelar o no para mantenerla."
    });

    /// <summary>
    /// Cancellation successful.
    /// </summary>
    public static string CancellationSuccess() => Pick(new[]
    {
        "Listo, tu reserva ha sido cancelada. ¡Esperamos verte pronto!",
        "Reserva cancelada correctamente. ¡Te esperamos otro día!",
        "Hecho, he cancelado tu reserva. ¡Hasta pronto!",
        "Tu reserva está cancelada. ¡Esperamos verte en otra ocasión!",
        "Perfecto, reserva cancelada. ¡Vuelve cuando quieras!",
        "Ya está, reserva cancelada. ¡Te esperamos!",
        "Cancelación completada. ¡Esperamos verte pronto!",
        "Tu reserva ha sido cancelada. ¡Hasta la próxima!",
        "Listo, cancelada. ¡Te esperamos otro día!",
        "Reserva cancelada. ¡Esperamos verte muy pronto!"
    });

    /// <summary>
    /// Cancellation aborted by user.
    /// </summary>
    public static string CancellationAborted() => Pick(new[]
    {
        "Perfecto, tu reserva sigue activa. ¡Te esperamos!",
        "Vale, no cancelamos nada. Tu reserva sigue en pie.",
        "Entendido, mantenemos tu reserva. ¡Nos vemos!",
        "Ok, dejamos la reserva como está. ¡Te esperamos!",
        "Muy bien, tu reserva sigue confirmada.",
        "Perfecto, no hay cambios. ¡Te esperamos el día de tu reserva!",
        "Vale, tu reserva se mantiene. ¡Hasta entonces!",
        "Entendido, no cancelamos. Tu reserva sigue activa.",
        "Ok, reserva mantenida. ¡Te esperamos!",
        "Muy bien, dejamos tu reserva. ¡Nos vemos!"
    });

    /// <summary>
    /// Cancellation database error.
    /// </summary>
    public static string CancellationError() => Pick(new[]
    {
        "Lo siento, hubo un error al cancelar la reserva. Por favor, inténtalo de nuevo.",
        "Vaya, no se pudo cancelar la reserva. ¿Puedes intentarlo otra vez?",
        "Ups, hubo un problema al cancelar. Por favor, inténtalo de nuevo.",
        "Lo siento, falló la cancelación. Vuelve a intentarlo.",
        "No se ha podido cancelar la reserva. Por favor, inténtalo otra vez.",
        "Perdona, hubo un error cancelando. ¿Puedes volver a intentarlo?",
        "Vaya, algo falló al cancelar. Inténtalo de nuevo, por favor.",
        "Lo siento, la cancelación no se procesó. Por favor, vuelve a intentarlo.",
        "Hubo un problema al procesar la cancelación. Inténtalo otra vez.",
        "Ups, no se pudo cancelar. Por favor, inténtalo de nuevo."
    });

    /// <summary>
    /// Cancellation confirmation not understood (specific for cancellation flow).
    /// </summary>
    public static string CancellationConfirmationNotUnderstood() => Pick(new[]
    {
        "Por favor, confirma con *Sí* para cancelar o *No* para mantener tu reserva.",
        "No entendí. ¿Cancelamos? Responde *Sí* o *No*.",
        "¿Sí o no? Dime si quieres cancelar la reserva.",
        "Perdona, no pillé tu respuesta. ¿*Sí* para cancelar o *No* para mantenerla?",
        "¿Cancelamos la reserva? Dime *Sí* o *No*.",
        "No entendí bien. ¿*Sí* para cancelar o *No* para dejarla?",
        "¿Qué me dices? *Sí* para cancelar o *No* para mantener la reserva.",
        "Por favor, dime *Sí* para cancelar o *No* para mantenerla.",
        "No me quedó claro. ¿Cancelamos? *Sí* o *No*.",
        "¿Confirmas la cancelación? Responde *Sí* o *No*."
    });

    // ========== BOOKING ERROR RESPONSES ==========

    /// <summary>
    /// Booking creation failed.
    /// </summary>
    public static string BookingCreationFailed() => Pick(new[]
    {
        "No se pudo crear la reserva. Por favor, inténtalo de nuevo o llámanos al +34 638 857 294.",
        "Vaya, hubo un error creando la reserva. ¿Puedes intentarlo otra vez o llamarnos al +34 638 857 294?",
        "Lo siento, no se pudo guardar la reserva. Inténtalo de nuevo o llámanos al +34 638 857 294.",
        "Ups, falló al crear la reserva. Por favor, prueba otra vez o llámanos al +34 638 857 294.",
        "No se ha podido procesar la reserva. Inténtalo de nuevo o contacta con nosotros al +34 638 857 294.",
        "Ha habido un problema con la reserva. ¿Lo intentamos de nuevo? También puedes llamarnos al +34 638 857 294.",
        "Lo siento, la reserva no se guardó. Por favor, inténtalo otra vez o llámanos al +34 638 857 294.",
        "Vaya, algo falló. Prueba de nuevo o llámanos directamente al +34 638 857 294.",
        "No se pudo completar la reserva. Inténtalo otra vez o contacta al +34 638 857 294.",
        "Hubo un error con la reserva. Por favor, prueba de nuevo o llámanos al +34 638 857 294."
    });

    /// <summary>
    /// Error processing booking (exception).
    /// </summary>
    public static string BookingProcessingError() => Pick(new[]
    {
        "Error al procesar la reserva. Por favor, inténtalo de nuevo.",
        "Vaya, hubo un problema procesando tu reserva. ¿Puedes intentarlo otra vez?",
        "Lo siento, ocurrió un error. Por favor, inténtalo de nuevo.",
        "Ups, algo salió mal. Vuelve a intentarlo, por favor.",
        "Ha habido un error procesando. Inténtalo otra vez.",
        "Lo siento, falló el procesamiento. Por favor, prueba de nuevo.",
        "Vaya, no se pudo procesar. ¿Puedes intentarlo otra vez?",
        "Hubo un problema. Por favor, inténtalo de nuevo.",
        "Error inesperado. Vuelve a intentarlo, por favor.",
        "Lo siento, algo falló. Inténtalo otra vez."
    });

    // ========== SAME-DAY BOOKING RESPONSES ==========

    /// <summary>
    /// Same-day booking intro message (before sending contact card).
    /// </summary>
    public static string SameDayBookingIntro() => Pick(new[]
    {
        "Las reservas para el mismo día las gestionamos por teléfono para poder atenderte mejor. Te paso el contacto.",
        "Para reservar para hoy, mejor que hables directamente con nuestro equipo. Te envío su contacto.",
        "Las reservas del día de hoy las gestionamos por teléfono. Te paso el contacto de nuestro equipo.",
        "Para hoy mismo, nuestro equipo te atenderá mejor por teléfono. Te envío su contacto.",
        "Para reservas del mismo día, te pongo en contacto con nuestro equipo que te ayudará personalmente.",
        "Las reservas para hoy las hacemos por teléfono para confirmar disponibilidad. Te paso el contacto.",
        "Para hoy, mejor contacta directamente con nosotros. Te envío la tarjeta de contacto.",
        "Las reservas del mismo día requieren confirmación directa. Te paso el contacto de nuestro equipo.",
        "Para reservar para hoy, te pongo en contacto con nuestro equipo de reservas.",
        "Las reservas para el día de hoy las gestionamos personalmente. Te envío el contacto."
    });

    /// <summary>
    /// Same-day booking rejection (after contact card).
    /// </summary>
    public static string SameDayBookingRejection() => Pick(new[]
    {
        "Te he enviado la tarjeta de contacto. Llámanos y vemos disponibilidad para hoy. ¡Gracias!",
        "Ahí tienes el contacto. Llámanos y te atendemos para ver si hay hueco hoy. ¡Un saludo!",
        "Ya tienes el contacto. Llámanos para consultar disponibilidad para hoy. ¡Gracias!",
        "Te paso el contacto. Llámanos y te confirmamos si hay sitio para hoy. ¡Hasta pronto!",
        "Listo, te he enviado el contacto. Llámanos y miramos disponibilidad. ¡Gracias!",
        "Ahí va la tarjeta. Llámanos para ver si podemos atenderos hoy. ¡Un saludo!",
        "Te envío el contacto. Llámanos y comprobamos si hay mesa para hoy. ¡Gracias!",
        "Ya tienes la tarjeta. Contacta con nosotros para ver disponibilidad. ¡Hasta pronto!",
        "Te he pasado el contacto. Llámanos y te decimos si hay hueco. ¡Gracias!",
        "Ahí tienes la tarjeta de contacto. Llámanos y te atendemos. ¡Un saludo!"
    });

    // ========== RESTAURANT CLOSED RESPONSES ==========

    /// <summary>
    /// Restaurant closed on requested day.
    /// </summary>
    public static string RestaurantClosed(string closedDay) => Pick(new[]
    {
        $"Lo siento, estamos cerrados el {closedDay}. ¿Te viene bien otro día?",
        $"Vaya, el {closedDay} no abrimos. ¿Qué tal otro día?",
        $"El {closedDay} cerramos. ¿Quieres reservar para otro día?",
        $"Lo siento, el {closedDay} no estamos. ¿Te viene bien otra fecha?",
        $"Ese día ({closedDay}) estamos cerrados. ¿Probamos con otro?",
        $"El {closedDay} no abrimos. ¿Te gustaría reservar para otro día?",
        $"Vaya, ese día cerramos ({closedDay}). ¿Qué otro día te vendría bien?",
        $"Lo siento, no abrimos el {closedDay}. ¿Qué otra fecha te iría bien?",
        $"El {closedDay} no estamos disponibles. ¿Probamos con otro día?",
        $"Cerramos el {closedDay}. ¿Te gustaría reservar para otra fecha?"
    });

    /// <summary>
    /// Restaurant closed with next open day suggestion.
    /// </summary>
    public static string RestaurantClosedWithSuggestion(string closedDay, string nextOpenDay) => Pick(new[]
    {
        $"Lo siento, estamos cerrados el {closedDay}. ¿Te viene bien reservar para el {nextOpenDay}?",
        $"Vaya, el {closedDay} no abrimos. ¿Qué tal el {nextOpenDay}?",
        $"El {closedDay} cerramos. ¿Quieres reservar para el {nextOpenDay}?",
        $"Lo siento, el {closedDay} no estamos. ¿Te viene bien el {nextOpenDay}?",
        $"Ese día cerramos. ¿Te gustaría el {nextOpenDay}?",
        $"El {closedDay} no abrimos. ¿Probamos con el {nextOpenDay}?",
        $"Vaya, ese día no abrimos. ¿Te iría bien el {nextOpenDay}?",
        $"Lo siento, no abrimos el {closedDay}. ¿Y el {nextOpenDay}?",
        $"El {closedDay} no estamos. ¿Qué tal el {nextOpenDay}?",
        $"Cerramos el {closedDay}. ¿Te vendría bien el {nextOpenDay}?"
    });

    // ========== LARGE GROUP / SPECIAL REQUEST INTRO ==========

    /// <summary>
    /// Large group intro message (before sending contact card).
    /// </summary>
    public static string LargeGroupIntro() => Pick(new[]
    {
        "Para grupos de más de 10 personas, te pongo en contacto con nuestro equipo de reservas que te ayudará personalmente.",
        "Los grupos de más de 10 personas los gestionamos de forma personalizada. Te paso el contacto de nuestro equipo.",
        "Para grupos grandes te atiende mejor nuestro equipo de reservas. Te envío su contacto.",
        "Para más de 10 personas, mejor que hables con nuestro equipo directamente. Te paso su contacto.",
        "Grupos de más de 10 personas los llevamos de forma especial. Te pongo en contacto con nuestro equipo.",
        "Para un grupo tan grande, nuestro equipo de reservas te atenderá mejor. Te paso el contacto.",
        "Los grupos numerosos los gestionamos de forma personalizada. Te envío el contacto de nuestro equipo.",
        "Para más de 10 personas, te pongo en contacto directo con nuestro equipo de reservas.",
        "Grupos grandes requieren atención especial. Te paso el contacto de nuestro equipo.",
        "Para ese número de personas, mejor contacta con nuestro equipo de reservas. Te envío sus datos."
    });

    /// <summary>
    /// Special request intro message (before sending contact card).
    /// </summary>
    public static string SpecialRequestIntro() => Pick(new[]
    {
        "Para solicitudes especiales como tartas de cumpleaños, celebraciones o eventos privados, te pongo en contacto con nuestro equipo que te ayudará personalmente.",
        "Las solicitudes especiales las gestiona nuestro equipo directamente. Te paso su contacto.",
        "Para tartas, celebraciones o eventos, mejor que hables con nuestro equipo. Te envío su contacto.",
        "Eso lo gestiona mejor nuestro equipo de eventos. Te paso su contacto.",
        "Para solicitudes especiales, te pongo en contacto con quien mejor te puede ayudar.",
        "Las celebraciones y eventos los organizamos de forma personalizada. Te paso el contacto de nuestro equipo.",
        "Para peticiones especiales, nuestro equipo te atenderá mejor. Te envío su contacto.",
        "Eso requiere atención especial. Te pongo en contacto con nuestro equipo de eventos.",
        "Para tartas y celebraciones, mejor que hables directamente con nuestro equipo. Aquí tienes su contacto.",
        "Las solicitudes así las gestionamos de forma personalizada. Te paso el contacto de nuestro equipo."
    });

    // ========== GENERIC ERROR RESPONSES ==========

    /// <summary>
    /// Generic assistant error (fallback).
    /// </summary>
    public static string GenericError() => Pick(new[]
    {
        "Disculpa, hubo un problema con el asistente. Por favor, llámanos al +34 638 857 294.",
        "Lo siento, algo ha fallado. ¿Puedes llamarnos al +34 638 857 294?",
        "Vaya, ha habido un error. Llámanos al +34 638 857 294 y te atendemos.",
        "Perdona, algo salió mal. Por favor, contacta con nosotros al +34 638 857 294.",
        "Ha ocurrido un error. Llámanos al +34 638 857 294 para ayudarte.",
        "Lo siento, hubo un problema. ¿Puedes llamar al +34 638 857 294?",
        "Disculpa, algo no funcionó bien. Llámanos al +34 638 857 294.",
        "Vaya, falló algo. Por favor, llámanos al +34 638 857 294.",
        "Perdona, hubo un error técnico. Contacta con nosotros al +34 638 857 294.",
        "Lo siento, algo ha ido mal. Llámanos al +34 638 857 294 para ayudarte mejor."
    });

    /// <summary>
    /// Fallback when AI doesn't understand (no valid response).
    /// </summary>
    public static string FallbackNotUnderstood() => Pick(new[]
    {
        "Disculpa, no he entendido bien. ¿Puedes repetirlo?",
        "Perdona, no te he pillado. ¿Puedes decirlo de otra forma?",
        "No he entendido bien. ¿Me lo repites?",
        "Lo siento, no he captado lo que dices. ¿Puedes repetirlo?",
        "Perdona, no lo he entendido. ¿Puedes decirlo de nuevo?",
        "No te he pillado bien. ¿Me lo explicas de otra forma?",
        "Disculpa, no he entendido. ¿Puedes repetir?",
        "Lo siento, no he captado eso. ¿Me lo dices otra vez?",
        "Perdona, no te entendí. ¿Puedes reformularlo?",
        "No he entendido bien tu mensaje. ¿Puedes repetirlo?"
    });

    // ========== RICE VALIDATION RESPONSES ==========

    /// <summary>
    /// Rice type not found on menu.
    /// </summary>
    public static string RiceTypeNotFound() => Pick(new[]
    {
        "Lo siento, no tenemos ese tipo de arroz. ¿Te gustaría ver nuestros arroces disponibles?",
        "Vaya, ese arroz no está en nuestra carta. ¿Quieres ver qué arroces tenemos?",
        "No tenemos ese arroz. ¿Te muestro los que sí tenemos?",
        "Ese tipo de arroz no lo tenemos. ¿Quieres saber cuáles sí tenemos?",
        "Lo siento, no servimos ese arroz. ¿Te interesa ver nuestras opciones?",
        "No está ese arroz en nuestro menú. ¿Te cuento qué arroces tenemos?",
        "Vaya, ese arroz no lo servimos. ¿Quieres ver nuestros arroces?",
        "No tenemos ese tipo de arroz. ¿Te gustaría conocer los que sí ofrecemos?",
        "Lo siento, ese arroz no está disponible. ¿Te muestro los que tenemos?",
        "No servimos ese arroz. ¿Te interesa ver qué opciones tenemos?"
    });

    /// <summary>
    /// Rice not on menu with specific type mentioned.
    /// </summary>
    public static string RiceNotOnMenu(string requestedRice, string availableTypes) => Pick(new[]
    {
        $"Lo siento, no tenemos \"{requestedRice}\" en nuestro menú. Nuestros arroces disponibles son: {availableTypes}. ¿Te gustaría alguno de estos?",
        $"Vaya, \"{requestedRice}\" no está en nuestra carta. Tenemos: {availableTypes}. ¿Te apetece alguno?",
        $"No tenemos \"{requestedRice}\". Nuestros arroces son: {availableTypes}. ¿Cuál prefieres?",
        $"Ese arroz (\"{requestedRice}\") no lo tenemos. Disponemos de: {availableTypes}. ¿Alguno te interesa?",
        $"Lo siento, \"{requestedRice}\" no está disponible. Tenemos: {availableTypes}. ¿Te gustaría probar alguno?",
        $"No servimos \"{requestedRice}\". Nuestras opciones son: {availableTypes}. ¿Cuál te apetece?",
        $"Vaya, no tenemos \"{requestedRice}\". Los arroces que ofrecemos son: {availableTypes}. ¿Cuál prefieres?",
        $"Ese tipo de arroz no lo tenemos. Puedes elegir entre: {availableTypes}. ¿Alguno te gusta?",
        $"Lo siento, \"{requestedRice}\" no está en el menú. Tenemos: {availableTypes}. ¿Te interesa alguno?",
        $"No disponemos de \"{requestedRice}\". Nuestros arroces son: {availableTypes}. ¿Cuál te gustaría?"
    });

    /// <summary>
    /// Multiple rice options found - asking for clarification.
    /// </summary>
    public static string MultipleRiceOptions(string options) => Pick(new[]
    {
        $"Tenemos varias opciones: {options}. ¿Cuál prefieres?",
        $"Hay varias opciones de arroz: {options}. ¿Cuál te apetece?",
        $"Tenemos: {options}. ¿Cuál te gustaría?",
        $"Disponemos de varios: {options}. ¿Cuál eliges?",
        $"Nuestras opciones son: {options}. ¿Cuál prefieres?",
        $"Tenemos estos arroces: {options}. ¿Cuál te apetece más?",
        $"Hay varios tipos: {options}. ¿Cuál te gusta más?",
        $"Puedes elegir entre: {options}. ¿Cuál prefieres?",
        $"Tenemos varias opciones de arroz: {options}. ¿Cuál eliges?",
        $"Las opciones son: {options}. ¿Cuál te gustaría?"
    });

    // ========== BOOKING AVAILABILITY RESPONSES ==========

    /// <summary>
    /// Asking for party size when not provided or invalid.
    /// </summary>
    public static string AskPartySize() => Pick(new[]
    {
        "¿Para cuántas personas sería la reserva?",
        "¿Cuántas personas seréis?",
        "¿Para cuántos comensales?",
        "¿Cuántos vais a venir?",
        "¿Para cuántas personas os reservo?",
        "Dime, ¿cuántas personas seréis?",
        "¿Cuántos seréis en total?",
        "¿Para cuántos hago la reserva?",
        "¿Cuántas personas vendréis?",
        "¿Para cuántos comensales sería?"
    });

    /// <summary>
    /// Incomplete booking data - asking for confirmation of missing fields.
    /// </summary>
    public static string IncompleteBookingPrompt() => Pick(new[]
    {
        "Perfecto. ¿Me confirmas *fecha*, *hora* y *personas* para la reserva?",
        "Vale. Necesito que me confirmes *fecha*, *hora* y *número de personas*.",
        "Genial. ¿Me dices *fecha*, *hora* y *cuántas personas* seréis?",
        "De acuerdo. Dime *fecha*, *hora* y *personas* para completar la reserva.",
        "Perfecto. ¿Qué *fecha*, *hora* y para *cuántas personas*?",
        "Vale. ¿Me confirmas los datos? *Fecha*, *hora* y *número de personas*.",
        "Genial. Para la reserva necesito: *fecha*, *hora* y *personas*.",
        "De acuerdo. ¿Qué *día*, a qué *hora* y *cuántas personas* seréis?",
        "Perfecto. Dime *fecha*, *hora* y *número de comensales*.",
        "Vale. ¿Me indicas *fecha*, *hora* y *personas* para la reserva?"
    });

    private static string Pick(string[] options) =>
        options[_random.Next(options.Length)];
}
